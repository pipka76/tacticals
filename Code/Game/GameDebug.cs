using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

public partial class GameDebug : Node
{
	/// <summary>A view cone as it really is: the eye, and where each ray across it stopped, left to right.</summary>
	private class SightRecord
	{
		public Vector3 Eye { get; set; }
		public Vector3[] Edge { get; set; }
	}

	private class PathRecord
	{
		public Vector3[] Points { get; set; }
		public bool Loop { get; set; }
	}

	private List<SightRecord> _sightRegister = new List<SightRecord>();
	private List<PathRecord> _patrolRegister = new List<PathRecord>();
	// Rect2 is a struct, so these two stay allocation-free even with thousands of cells a frame.
	private List<Rect2> _blockedAreaRegister = new List<Rect2>();
	private List<Rect2> _blockedCellRegister = new List<Rect2>();
	private ImmediateMesh _immediateMesh;
	private MeshInstance3D _meshInstance;
	/// <summary>See-through fills go in a mesh of their own: the line mesh is opaque and draws lines only.</summary>
	private ImmediateMesh _fillMesh;
	private MeshInstance3D _fillInstance;

	private const float BLOCKED_LIFT = 0.15f;    // keep the overlay off the terrain surface
	private const float BLOCKED_HATCH_SPACING = 1.5f;
	private const float BLOCKED_POST_HEIGHT = 2.0f;

	/// <summary>One ray per this many degrees of a view cone - fine enough to show a window slit's edges.</summary>
	private const float SIGHT_RAY_STEP = 3f;
	private const int SIGHT_MIN_RAYS = 8;
	private static readonly Color SIGHT_OUTLINE = new Color(1f, 1f, 1f, 1f);
	private static readonly Color SIGHT_FILL = new Color(1f, 1f, 1f, 0.12f);

	public static GameDebug Current {  get; internal set; }

	/// <summary>
	/// Whether the debug overlay is currently being drawn. Check this before doing expensive
	/// work to build a registration - registers are filled every frame regardless of the toggle.
	/// </summary>
	public bool IsEnabled => PlayerInput.Current?.DebugToggle ?? false;

	public override void _Ready()
	{
		Current = this;

		_immediateMesh = new ImmediateMesh();
		_meshInstance = new MeshInstance3D();
		_meshInstance.Mesh = _immediateMesh;
		
		StandardMaterial3D material = new StandardMaterial3D();
		material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		material.VertexColorUseAsAlbedo = true;
		_meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		material.DisableReceiveShadows = true;
		_meshInstance.MaterialOverride = material;
		
		AddChild(_meshInstance);

		_fillMesh = new ImmediateMesh();
		_fillInstance = new MeshInstance3D
		{
			Mesh = _fillMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				VertexColorUseAsAlbedo = true,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				// Overlapping cones would otherwise hide each other half the time.
				DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
				DisableReceiveShadows = true
			}
		};
		AddChild(_fillInstance);
	}

	/// <summary>
	/// Draws what a unit can actually see this frame: a fan of rays across its view cone, each
	/// cut short where the environment (terrain, trees, walls - collision layer 1) blocks it, drawn
	/// as a filled wedge with that ragged far edge. The rays are only cast while the overlay is on.
	/// </summary>
	/// <param name="forward">Middle of the cone; flattened, the fan is level with the eye.</param>
	/// <param name="exclude">Colliders the unit looks out from, e.g. its own body.</param>
	public void RegisterSight(PhysicsDirectSpaceState3D space, Vector3 eye, Vector3 forward, float angle, float distance,
		Godot.Collections.Array<Rid> exclude = null)
	{
		if (!IsEnabled || space == null)
			return;

		forward.Y = 0f;
		if (forward.LengthSquared() < 0.000001f)
			return;
		forward = forward.Normalized();

		int rays = Math.Max(SIGHT_MIN_RAYS, Mathf.CeilToInt(angle / SIGHT_RAY_STEP)) + 1;
		float half = Mathf.DegToRad(angle / 2f);
		var edge = new Vector3[rays];
		var query = new PhysicsRayQueryParameters3D { From = eye, CollisionMask = 1 };
		if (exclude != null)
			query.Exclude = exclude;

		for (int i = 0; i < rays; i++)
		{
			// Left to right: positive turns about Up go left.
			float a = half - (2f * half) * i / (rays - 1);
			query.To = eye + forward.Rotated(Vector3.Up, a) * distance;
			var hit = space.IntersectRay(query);
			edge[i] = hit.Count > 0 ? (Vector3)hit["position"] : query.To;
		}

		_sightRegister.Add(new SightRecord { Eye = eye, Edge = edge });
	}

	public override void _Process(double delta)
	{
		if (PlayerInput.Current == null)
			return;

		_immediateMesh.ClearSurfaces();
		_fillMesh.ClearSurfaces();
		if (PlayerInput.Current.DebugToggle)
		{
			_immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
			
			DrawSightOutlines();
			DrawPatrolPaths();
			DrawBlockedAreas();

			_immediateMesh.SurfaceEnd();

			DrawSightFills();
		}
		_sightRegister.Clear();
		_patrolRegister.Clear();
		_blockedAreaRegister.Clear();
		_blockedCellRegister.Clear();
	}

	private void DrawPatrolPaths()
	{
		if (_patrolRegister.Count == 0)
			return;

		foreach (var record in _patrolRegister)
		{
			var points = record.Points;
			if (points == null || points.Length < 2)
				continue;

			// A one-shot move route stops at its last waypoint; a patrol circuit closes the loop.
			int segments = record.Loop ? points.Length : points.Length - 1;
			var color = record.Loop ? Colors.Purple : Colors.Cyan;

			for (int i = 0; i < segments; i++)
			{
				DrawLine(points[i] + Vector3.Up * 0.1f, points[(i + 1) % points.Length] + Vector3.Up * 0.1f, color);
			}
		}
	}

	private void DrawLine(Vector3 from, Vector3 to, Color color)
	{
		_immediateMesh.SurfaceSetColor(color);
		_immediateMesh.SurfaceAddVertex(from);
		_immediateMesh.SurfaceAddVertex(to);
	}

	/// <summary>A closed patrol circuit - the last point links back to the first.</summary>
	public void RegisterPatrolPath(Vector3[] points)
	{
		RegisterPath(points, true);
	}

	/// <summary>
	/// Draws a route for one frame. Set <paramref name="loop"/> false for a one-shot move order,
	/// which ends at its last waypoint instead of closing back on itself.
	/// </summary>
	public void RegisterPath(Vector3[] points, bool loop)
	{
		_patrolRegister.Add(new PathRecord { Points = points, Loop = loop });
	}

	/// <summary>
	/// Draws a world-space footprint as a hatched, fenced area for one frame.
	/// This shows the area you ASKED to block. To see what is actually blocked after the
	/// footprint has been rasterised onto the pathfinding grid, use RegisterBlockedCells -
	/// the two differ whenever a rect does not land on cell boundaries.
	/// </summary>
	public void RegisterBlockedArea(Rect2 worldArea)
	{
		_blockedAreaRegister.Add(worldArea);
	}

	/// <summary>
	/// Draws every grid cell currently closed to <paramref name="domain"/> - the ground truth
	/// that units actually collide with, rasterisation and all. This is the one to trust when
	/// checking placement.
	/// Skips the whole scan when the overlay is off, so it is safe to call every frame.
	/// </summary>
	public void RegisterBlockedCells(FlowFieldManager pathField, MovementDomain domain, int maxCells = 3000)
	{
		if (!IsEnabled || pathField == null || !pathField.IsInitialized)
			return;

		float size = pathField.CellSize;
		var extent = new Vector2(size, size);

		for (int y = 0; y < pathField.Height; y++)
		{
			for (int x = 0; x < pathField.Width; x++)
			{
				var cell = new Vector2I(x, y);
				if (pathField.IsPassable(cell, domain))
					continue;

				if (_blockedCellRegister.Count >= maxCells)
					return;      // truncate rather than stall the frame

				_blockedCellRegister.Add(new Rect2(pathField.CellToWorldCenter(cell) - extent * 0.5f, extent));
			}
		}
	}

	private void DrawBlockedAreas()
	{
		foreach (var area in _blockedAreaRegister)
		{
			DrawRectOutline(area, Colors.OrangeRed);
			DrawRectHatch(area, BLOCKED_HATCH_SPACING, Colors.OrangeRed);
			DrawCornerPosts(area, Colors.OrangeRed);
		}

		// Cells are small and numerous: outline plus a single diagonal reads as hatching at
		// map scale without flooding the mesh.
		foreach (var cell in _blockedCellRegister)
		{
			DrawRectOutline(cell, Colors.Orange);
			DrawGroundLine(cell.Position, cell.End, Colors.Orange);
		}
	}

	private void DrawRectOutline(Rect2 area, Color color)
	{
		var a = area.Position;
		var b = new Vector2(area.End.X, area.Position.Y);
		var c = area.End;
		var d = new Vector2(area.Position.X, area.End.Y);

		DrawGroundLine(a, b, color);
		DrawGroundLine(b, c, color);
		DrawGroundLine(c, d, color);
		DrawGroundLine(d, a, color);
	}

	/// <summary>45-degree hatching, clipped to the rect. Hatch lines satisfy x - z = k.</summary>
	private void DrawRectHatch(Rect2 area, float spacing, Color color)
	{
		float x0 = area.Position.X, x1 = area.End.X;
		float z0 = area.Position.Y, z1 = area.End.Y;

		for (float k = x0 - z1; k <= x1 - z0; k += spacing)
		{
			if (TryClipDiagonal(x0, x1, z0, z1, k, out var from, out var to))
				DrawGroundLine(from, to, color);
		}
	}

	/// <summary>Vertical posts at the corners, so the area stays readable from a top-down camera.</summary>
	private void DrawCornerPosts(Rect2 area, Color color)
	{
		Vector2[] corners =
		{
			area.Position,
			new Vector2(area.End.X, area.Position.Y),
			area.End,
			new Vector2(area.Position.X, area.End.Y)
		};

		foreach (var corner in corners)
		{
			float y = GroundAt(corner);
			DrawLine(new Vector3(corner.X, y, corner.Y), new Vector3(corner.X, y + BLOCKED_POST_HEIGHT, corner.Y), color);
		}
	}

	/// <summary>Clips the line x - z = k to the rect, returning the two edge crossings.</summary>
	private static bool TryClipDiagonal(float x0, float x1, float z0, float z1, float k, out Vector2 from, out Vector2 to)
	{
		Span<Vector2> hits = stackalloc Vector2[4];
		int n = 0;

		// Strict inequalities on the horizontal edges so a corner is not counted twice.
		float z = x0 - k;
		if (z >= z0 && z <= z1) hits[n++] = new Vector2(x0, z);
		z = x1 - k;
		if (z >= z0 && z <= z1) hits[n++] = new Vector2(x1, z);
		float x = k + z0;
		if (x > x0 && x < x1) hits[n++] = new Vector2(x, z0);
		x = k + z1;
		if (x > x0 && x < x1) hits[n++] = new Vector2(x, z1);

		if (n < 2)
		{
			from = default;
			to = default;
			return false;
		}

		from = hits[0];
		to = hits[1];
		return (from - to).LengthSquared() > 0.0001f;
	}

	/// <summary>Drapes a flat XZ segment onto the terrain so the overlay follows hills.</summary>
	private void DrawGroundLine(Vector2 from, Vector2 to, Color color)
	{
		DrawLine(new Vector3(from.X, GroundAt(from), from.Y), new Vector3(to.X, GroundAt(to), to.Y), color);
	}

	private static float GroundAt(Vector2 worldXZ)
	{
		return (Main3d.Current?.Map?.GetTerrainHeight(worldXZ) ?? 0f) + BLOCKED_LIFT;
	}

	/// <summary>The wedge's border: both sides and the far edge where the rays stopped.</summary>
	private void DrawSightOutlines()
	{
		foreach (var record in _sightRegister)
		{
			var edge = record.Edge;
			DrawLine(record.Eye, edge[0], SIGHT_OUTLINE);
			for (int i = 1; i < edge.Length; i++)
				DrawLine(edge[i - 1], edge[i], SIGHT_OUTLINE);
			DrawLine(edge[^1], record.Eye, SIGHT_OUTLINE);
		}
	}

	/// <summary>The see-through inside of each wedge, as a fan of triangles from the eye.</summary>
	private void DrawSightFills()
	{
		if (_sightRegister.Count == 0)
			return;

		_fillMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		foreach (var record in _sightRegister)
		{
			var edge = record.Edge;
			for (int i = 1; i < edge.Length; i++)
			{
				_fillMesh.SurfaceSetColor(SIGHT_FILL);
				_fillMesh.SurfaceAddVertex(record.Eye);
				_fillMesh.SurfaceAddVertex(edge[i - 1]);
				_fillMesh.SurfaceAddVertex(edge[i]);
			}
		}
		_fillMesh.SurfaceEnd();
	}
}
