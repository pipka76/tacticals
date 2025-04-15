using Godot;
using System;

public partial class Player : Node3D
{
	// Set by the authority, synchronized on spawn.
	[Export]
	public int PlayerId
	{
		get => _playerId;
		set
		{
			_playerId = value;
			// Give authority over the player input to the appropriate peer.
			GetNode("PlayerInput").SetMultiplayerAuthority(value);
		}
	}

	private int _playerId = 1;	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
