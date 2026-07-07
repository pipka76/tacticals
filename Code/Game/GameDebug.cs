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

	private List<FovRecord> _fovRegister = new List<FovRecord>();
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
			DrawFOV();
		}
		_fovRegister.Clear();
	}

	private void DrawLine(Vector3 from, Vector3 to, Color color)
	{
		_immediateMesh.SurfaceSetColor(color);
		_immediateMesh.SurfaceAddVertex(from);
		_immediateMesh.SurfaceAddVertex(to);
	}

	public void DrawFOV()
	{
		if (_fovRegister.Count == 0)
			return;

		_immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

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

		_immediateMesh.SurfaceEnd();
	}
}
