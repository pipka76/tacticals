using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

public partial class GameDebug : Node
{
	public class FovRecord
	{ 
		public Vector3 From {  get; set; }
		public float FovAngle { get; set; }
		public Vector3 Forward { get; set; }
		public float FovDistance { get; set; }
	}

	private class PathRecord
	{
		public Vector3[] Points { get; set; }
		public bool Loop { get; set; }
	}

	private List<FovRecord> _fovRegister = new List<FovRecord>();
	private List<PathRecord> _patrolRegister = new List<PathRecord>();
	// Rect2 is a struct, so these two stay allocation-free even with thousands of cells a frame.
	private List<Rect2> _blockedAreaRegister = new List<Rect2>();
	private List<Rect2> _blockedCellRegister = new List<Rect2>();
    private ImmediateMesh _immediateMesh;
	private MeshInstance3D _meshInstance;

	private const float BLOCKED_LIFT = 0.15f;    // keep the overlay off the terrain surface
	private const float BLOCKED_HATCH_SPACING = 1.5f;
	private const float BLOCKED_POST_HEIGHT = 2.0f;

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
	}

	public void RegisterFov(Vector3 from, float angle, Vector3 forward, float distance)
	{ 
		_fovRegister.Add(new GameDebug.FovRecord() { From = from, FovAngle = angle, Forward = forward, FovDistance = distance});
	}

	public override void _Process(double delta)
	{
		if (PlayerInput.Current == null)
			return;

		_immediateMesh.ClearSurfaces();
		if (PlayerInput.Current.DebugToggle)
		{
            _immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            
			DrawFOV();
			DrawPatrolPaths();
			DrawBlockedAreas();

            _immediateMesh.SurfaceEnd();
        }
        _fovRegister.Clear();
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

    private void DrawFOV()
	{
		if (_fovRegister.Count == 0)
			return;

		foreach (var record in _fovRegister)
		{
			float halfAngle = Mathf.DegToRad(record.FovAngle / 2f);

			Vector3 right = record.Forward.Cross(Vector3.Up).Normalized();
			Vector3 rotationAxis = right.Cross(record.Forward).Normalized();

			Vector3 leftEdge = record.Forward.Rotated(rotationAxis, -halfAngle).Normalized();
			Vector3 rightEdge = record.Forward.Rotated(rotationAxis, halfAngle).Normalized();

			float rayLength = record.FovDistance;
			DrawLine(record.From, record.From + leftEdge * rayLength, Colors.White);
			DrawLine(record.From, record.From + rightEdge * rayLength, Colors.White);
		}
	}
}
