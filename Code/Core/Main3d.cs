using Godot;
using System;
using System.Linq;
using System.Text;
using tacticals.Code.Game;

public partial class Main3d : Node
{
	public enum NAVIGATE_TARGET
	{
		STARTMENU,
		LOBBYMENU,
		BATTLEMENU
	}

	private const int PORT = 20000;
	private const string PORT_ARG = "--port=";
	public static Main3d Current { get; internal set; }

	// Join ticket sent to the game server in the ENet auth handshake (see JoinServer, ServerAuth).
	private string _joinTicket;
	
	public AudioManager Audio { get; internal set; }
	
	public ProjectileManager Projectiles { get; internal set; }

	/// <summary>
	/// The level currently loaded under the "Map" node, or null in the menus and during a
	/// scene swap. Entities should reach the map through this rather than walking the node tree.
	/// </summary>
	public IGameMap Map { get; private set; }

	public override void _Ready()
	{
		Current = this;
		// Automatically start the server in headless mode.
		if (DisplayServer.GetName() == "headless")
		{
			int port = PORT;
			// User args after "--" are only reported by GetCmdlineUserArgs.
			var args = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs());
			var sPort= args.FirstOrDefault(a => a.StartsWith(PORT_ARG));
			if (!String.IsNullOrEmpty(sPort))
			{
				GD.Print($"Found port parameter: {sPort}");
				if (!int.TryParse(sPort.Substring(PORT_ARG.Length), out port))
					port = PORT;
			}

			GD.Print($"Automatically starting dedicated server on port {port}");
			CallDeferred(nameof(StartServer), port);
			AddChild(new ServerLifecycle());
			AddChild(new ServerAuth());
		}
		else
		{
			// Game servers only accept peers that authenticate with a join ticket.
			var multiplayer = (SceneMultiplayer)Multiplayer;
			multiplayer.AuthCallback = Callable.From<long, byte[]>((_, _) => { });
			multiplayer.AuthTimeout = ServerAuth.AUTH_TIMEOUT_SEC;
			multiplayer.PeerAuthenticating += OnPeerAuthenticating;
		}

		Audio = this.GetNode<AudioManager>("Audio");
		Projectiles = this.GetNode<ProjectileManager>("Projectiles");
	}
	
	public void StartServer(int port)
	{
		// Start as server
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateServer(port);

		if (error != Error.Ok || peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			OS.Alert("Failed to start multiplayer server");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		
		// The API server waits for this exact line before handing the endpoint to players.
		GD.Print("Server Started!");
	}
	
	public IGameMap StartGame(string mapScene)
	{
//		if (Multiplayer.IsServer())
//		{
	//		CallDeferred(nameof(ChangeLevel), GD.Load<PackedScene>(mapScene));
//		}
		(GetNode("MenuBackground") as Node3D).Visible = false;
		return ChangeLevel(GD.Load<PackedScene>(mapScene));
	}
	
	/// <summary>Connects to a battle's game server using the endpoint from the API.</summary>
	public void JoinServer(string host, int port, string ticket)
	{
		_joinTicket = ticket;
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateClient(host, port);

		if (error != Error.Ok || peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			OS.Alert("Failed to start multiplayer client");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		
		//StartGame();
	}

	private void OnPeerAuthenticating(long id)
	{
		// The server verifies the ticket; there is nothing for us to check on our side.
		var multiplayer = (SceneMultiplayer)Multiplayer;
		multiplayer.SendAuth((int)id, Encoding.UTF8.GetBytes(_joinTicket ?? string.Empty));
		multiplayer.CompleteAuth((int)id);
	}

	public void NavigateTo(NAVIGATE_TARGET target, NavigateContext context = null)
	{
		var main = (GetNode("CanvasLayer/StartMenu") as StartMenu);
		var lobby = (GetNode("CanvasLayer/LobbyMenu") as LobbyMenu);
		var battle = (GetNode("CanvasLayer/BattleMenu") as BattleMenu);
		
		switch (target)
		{
			case NAVIGATE_TARGET.STARTMENU:
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

	private IGameMap ChangeLevel(PackedScene scene)
	{
		// Drop the handle before freeing, so nothing dereferences a disposed level mid-teardown.
		Map = null;

		// Remove old level if any.
		var mapRoot = GetNode("Map");
		foreach (Node child in mapRoot.GetChildren())
		{
			mapRoot.RemoveChild(child);
			child.QueueFree();
		}

		// Add new level.
		var li = scene.Instantiate();
		if (li is IGameMap map)
		{
			mapRoot.AddChild(li);
			map.GenerateLevel();

			Map = map;
			return map;
		}

		return null;
	}
}
