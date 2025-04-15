using Godot;
using System;

public partial class Main : Node
{
	private const int PORT = 50000;
	
	public override void _Ready()
	{
		// Automatically start the server in headless mode.
		if (DisplayServer.GetName() == "headless")
		{
			GD.Print("Automatically starting dedicated server");
			CallDeferred(nameof(StartServer));
		}
	}
	
	public void StartServer()
	{
		// Start as server
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateServer(PORT);

		if (error != Error.Ok || peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			OS.Alert("Failed to start multiplayer server");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		StartGame();

		GD.Print("Server Started!");
	}
	
	public void JoinServer(string ip)
	{
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateClient(ip, PORT);

		if (error != Error.Ok || peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			OS.Alert("Failed to start multiplayer client");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		StartGame();
	}
	
	private void OnConnectedToServer()
	{
		GD.Print("Connected successfully to the server.");
		StartGame();
	}
	
	private void StartGame()
	{
		(GetNode("MainMenu") as Control).Hide();
		//GetTree().Paused = false;
        
		// Only change map on the server.
		// Clients will instantiate the map via the spawner.
		if (Multiplayer.IsServer())
		{
			CallDeferred(nameof(ChangeLevel), GD.Load<PackedScene>("res://Scenes/Maps/Plains.tscn"));
		}
	}
	
	private void ChangeLevel(PackedScene scene)
	{
		// Remove old level if any.
		var mapRoot = GetNode("Map");
		foreach (Node child in mapRoot.GetChildren())
		{
			mapRoot.RemoveChild(child);
			child.QueueFree();
		}

		// Add new level.
		var li = scene.Instantiate();
		mapRoot.AddChild(li);
	}
}
