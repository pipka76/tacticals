using Godot;
using System;

public partial class Plains : Node3D
{
	private PackedScene _playerScene = GD.Load<PackedScene>("res://Scenes/Game/Player.tscn");
	
	public override void _Ready()
	{
		if (!Multiplayer.IsServer())
			return;

		Multiplayer.PeerConnected += AddPlayer;
		Multiplayer.PeerDisconnected += DelPlayer;

		// Spawn already connected players
		foreach (var id in Multiplayer.GetPeers())
		{
			AddPlayer(id);
		}

		// Spawn local player unless dedicated server
		if (!OS.HasFeature("dedicated_server"))
		{
			AddPlayer(1);
		}
	}
	
	public override void _ExitTree()
	{
		if (!Multiplayer.IsServer())
			return;

		Multiplayer.PeerConnected -= AddPlayer;
		Multiplayer.PeerDisconnected -= DelPlayer;
	}
	
	private void AddPlayer(long id)
	{
		var character = _playerScene.Instantiate<Node3D>();
        
		// Set player ID property (assuming your player scene has a "Player" script with a PlayerId property)
		character.Set("PlayerId", (int)id);

		// Randomize character position.
		//character.Position = GetRandomSpawnPoint();

		character.Name = id.ToString();

		GetNode("Players").AddChild(character, true);
	}

	private void DelPlayer(long id)
	{
		var playersNode = GetNode("Players");
		if (!playersNode.HasNode(id.ToString()))
			return;

		var playerNode = playersNode.GetNode(id.ToString());
		playerNode.QueueFree();
	}
}
