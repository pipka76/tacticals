using Godot;
using System;

namespace tacticals.Code.Game;

public partial class Soldier : MovableTeamEntity
{
	private const float MOVE_SPEED = 2.5f;
	private RayCast3D _rayCast;
	private AnimationPlayer _animPlayer;

	public Soldier()
	{
		_maxPassengersCapacity = 0;
	}
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_rayCast = new RayCast3D();
		_rayCast.CollisionMask = 0b1111;
		_rayCast.Enabled = true;
		
		_selectorObject = GetNode<Node3D>("SelectionRing");
		_synchronizer = GetNode<MultiplayerSynchronizer>("ServerSynchronizer");
		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//_synchronizer.SetVisibilityFor();
		AddChild(_rayCast);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var globalPositionFlat = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
		if (IsInState(TeamEntityStates.ONTHEWAY) && (MoveToCoordinates - globalPositionFlat).Length() > 0.1f)
		{
			if (RaycastToTerrain(out var gnd, out _))
			{
				var direction = (MoveToCoordinates - globalPositionFlat).Normalized();
				//_rayCast.GlobalPosition = GlobalPosition;
				//_rayCast.TargetPosition = GlobalPosition + direction * 30f;
				//if(!_rayCast.IsColliding())
				var moveXZ = GlobalPosition + direction * (float)delta * MOVE_SPEED;
				moveXZ.Y = gnd.Y;
				GlobalPosition = moveXZ;
			}
		}
		else
		{
			if (IsInState(TeamEntityStates.ONTHEWAY))
				SetNewState(TeamEntityStates.IDLE);
		}
		
		HandleAnimation();
	}

	private void HandleAnimation()
	{
		if (IsInState(TeamEntityStates.ONTHEWAY))
		{
			if (_animPlayer.CurrentAnimation != "Walking")
				_animPlayer.Play("Walking");
		}
		if (IsInState(TeamEntityStates.IDLE))
		{
			if (_animPlayer.CurrentAnimation != "Idle")
				_animPlayer.Play("Idle");
			if (RaycastToTerrain(out var gnd, out _))
				GlobalPosition = new Vector3(GlobalPosition.X, gnd.Y, GlobalPosition.Z);
		}
	}
}
