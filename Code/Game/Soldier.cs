using Godot;
using System;

public partial class Soldier : MovableTeamEntity
{
	private const float MOVE_SPEED = 1.5f; 
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_selectorObject = GetNode<Node3D>("SelectionRing");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if ((MoveToCoordinates - GlobalPosition).Length() > 0.1f)
		{
			GlobalPosition += (MoveToCoordinates - GlobalPosition).Normalized() * (float)delta * MOVE_SPEED;
		}
	}
}
