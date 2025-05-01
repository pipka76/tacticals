using Godot;
using System;
using System.Linq;

public partial class Main : Node
{
	public enum NAVIGATE_TARGET
	{
		MAINMENU,
		LOBBYMENU,
		BATTLEMENU
	}

	private const int PORT = 20000;
	private const string PORT_ARG = "--port=";
	public override void _Ready()
	{
		// Automatically start the server in headless mode.
		if (DisplayServer.GetName() == "headless")
		{
			int port = PORT;
			var args = OS.GetCmdlineArgs();
//			GD.Print($"Found port parameter: {args.Length}");
			var sPort= args.FirstOrDefault(a => a.StartsWith(PORT_ARG));
			if (!String.IsNullOrEmpty(sPort))
			{
				GD.Print($"Found port parameter: {sPort}");
				if (!int.TryParse(sPort.Substring(PORT_ARG.Length), out port))
					port = PORT;
			}

			GD.Print($"Automatically starting dedicated server on port {port}");
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
		
		GD.Print("Server Started!");
	}
	
	public void StartGame(string mapScene)
	{
		if (Multiplayer.IsServer())
		{
			CallDeferred(nameof(ChangeLevel), GD.Load<PackedScene>(mapScene));
		}
	}
	
	public void JoinServer(int port)
	{
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateClient("localhost", port);

		if (error != Error.Ok || peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			OS.Alert("Failed to start multiplayer client");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		
		//StartGame();
	}

	public void NavigateTo(NAVIGATE_TARGET target, NavigateContext context = null)
	{
		var main = (GetNode("MainMenu") as MainMenu);
		var lobby = (GetNode("LobbyMenu") as LobbyMenu);
		var battle = (GetNode("BattleMenu") as BattleMenu);
		
		switch (target)
		{
			case NAVIGATE_TARGET.MAINMENU:
				main.Show();
				main.OnNavigateTo(context);
				lobby.Hide();
				battle.Hide();
				break;
			case NAVIGATE_TARGET.LOBBYMENU:
				main.Hide();
				lobby.Show();
				lobby.OnNavigateTo(context);
				battle.Hide();
				break;
			case NAVIGATE_TARGET.BATTLEMENU:
				main.Hide();
				lobby.Hide();
				battle.Show();
				battle.OnNavigateTo(context);
				break;
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
		(li as IGameMap).GenerateLevel();
		mapRoot.AddChild(li);
	}
}
