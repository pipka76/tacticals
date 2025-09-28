using Godot;
using System;

public partial class Soldier : MovableTeamEntity
{
	private const float MOVE_SPEED = 1.5f;
	private MultiplayerSynchronizer _synchronizer;
	private RayCast3D _rayCast;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_rayCast = new RayCast3D();
		_rayCast.CollisionMask = 0b1111;
		_rayCast.Enabled = true;
		_selectorObject = GetNode<Node3D>("SelectionRing");
		_synchronizer = GetNode<MultiplayerSynchronizer>("ServerSynchronizer");
		//_synchronizer.SetVisibilityFor();
		AddChild(_rayCast);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if ((MoveToCoordinates - GlobalPosition).Length() > 0.1f)
		{
			var direction = (MoveToCoordinates - GlobalPosition).Normalized();
			_rayCast.GlobalPosition = GlobalPosition;
			_rayCast.TargetPosition = GlobalPosition + direction * 30f;
			if(!_rayCast.IsColliding())
				GlobalPosition += direction * (float)delta * MOVE_SPEED;
		}
	}
}
