using Godot;
using System;
using tacticals_api_server.Domain;

/// <summary>
/// Step 4 of the battle flow. Waits for the battle's game server (the owner starts it), connects
/// with a join ticket, shows the minimap of the map the server sends, and lets the player adjust
/// their army before Deploy. The battle begins (BattleNetwork.BeginBattle) once everyone deployed.
/// </summary>
public partial class PrepareForBattleMenu : Control, IGameMenu
{
	private Label _status;
	private Label _progress;
	private Button _deployButton;
	private TextureRect _minimap;
	private ArmyGrid _armyGrid;

	// One retry with a fresh ticket if the first connection attempt fails.
	private bool _retried;

	public override void _Ready()
	{
		_status = GetNode<Label>("MarginContainer/Prepare/Top/Info/Status");
		_progress = GetNode<Label>("MarginContainer/Prepare/Top/Info/Progress");
		_deployButton = GetNode<Button>("MarginContainer/Prepare/Top/Info/btnDeploy");
		_minimap = GetNode<TextureRect>("MarginContainer/Prepare/Top/MinimapFrame/Minimap");
		_armyGrid = GetNode<ArmyGrid>("MarginContainer/Prepare/ArmyGrid");

		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		BattleNetwork.Current.MapReceived += OnMapReceived;
		BattleNetwork.Current.DeployProgressChanged += OnDeployProgress;
	}

	public void OnNavigateTo(NavigateContext context)
	{
		_retried = false;
		_minimap.Texture = null;
		_deployButton.Disabled = true;
		_progress.Text = string.Empty;
		_armyGrid.SetLayout(Main3d.Current.Session.ArmyLayout);
		Connect();
	}

	private async void Connect()
	{
		var session = Main3d.Current.Session;
		BattleEndpoint endpoint;
		if (session.IsOwner && !_retried)
		{
			// Returns once the server is up; also hands out our join ticket.
			_status.Text = "Starting the game server...";
			endpoint = await BattleServer.Current.StartBattle(session.BattleId);
		}
		else
		{
			_status.Text = "Waiting for the game server...";
			endpoint = await BattleServer.Current.GetEndpoint(session.BattleId);
		}

		if (!this.Visible)
			return;

		if (endpoint == null)
		{
			Fail("The game server could not be started.");
			return;
		}

		_status.Text = $"Connecting to {endpoint.Host}:{endpoint.Port}...";
		Main3d.Current.JoinServer(endpoint.Host, endpoint.Port, endpoint.Ticket);
	}

	private void OnConnectedToServer()
	{
		if (!this.Visible)
			return;

		_status.Text = "Downloading the map...";
	}

	private void OnConnectionFailed()
	{
		if (!this.Visible)
			return;

		if (!_retried)
		{
			// Tickets are short-lived; a fresh one is fetched for the retry.
			_retried = true;
			Main3d.Current.LeaveServer();
			Connect();
			return;
		}

		Fail("Could not connect to the game server.");
	}

	private void OnServerDisconnected()
	{
		if (this.Visible)
			Fail("Lost the connection to the game server.");
	}

	private void OnMapReceived(MapBlock[][] map)
	{
		if (!this.Visible)
			return;

		_minimap.Texture = ImageTexture.CreateFromImage(Minimap.CreateImage(map));
		_deployButton.Disabled = false;
		_status.Text = "Choose your army for this map, then Deploy.";
	}

	private void OnDeployProgress(int deployed, int expected)
	{
		_progress.Text = $"Deployed: {deployed}/{expected}";
	}

	private void OnDeployPressed()
	{
		_deployButton.Disabled = true;
		Main3d.Current.Session.ArmyLayout = _armyGrid.GetLayout();
		BattleNetwork.Current.Deploy(_armyGrid.ToArmySetup());
		_status.Text = "Waiting for the other players...";
	}

	private void OnLeavePressed()
	{
		// The battle is already running, so the API won't take us out of it; leaving the game
		// server is what counts (it stops waiting for us).
		BackToLobby(null);
	}

	private void Fail(string message)
	{
		GD.Print($"PrepareForBattle: {message}");
		BackToLobby(message);
	}

	private void BackToLobby(string message)
	{
		Main3d.Current.LeaveServer();
		Main3d.Current.Session.LeaveBattle();
		var context = new NavigateContext();
		if (message != null)
			context.Metadata.Add(LobbyMenu.MESSAGE_META, message);
		Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.LOBBYMENU, context);
	}
}
