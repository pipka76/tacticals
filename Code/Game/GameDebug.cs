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
    private ImmediateMesh _immediateMesh;
	private MeshInstance3D _meshInstance;

	public static GameDebug Current {  get; internal set; }

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

            _immediateMesh.SurfaceEnd();
        }
        _fovRegister.Clear();
        _patrolRegister.Clear();
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
