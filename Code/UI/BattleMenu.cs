using Godot;
using System;
using System.Linq;

/// <summary>
/// Step 3 of the battle flow: the lobby of one battle. Polls the API for the players and their
/// ready state. The owner gets Start once everyone is ready, and everyone moves on to
/// PrepareForBattle once the battle is running.
/// </summary>
public partial class BattleMenu : Control, IGameMenu
{
	// A failed poll is retried; this many in a row means the battle is gone (e.g. the owner left).
	private const int MAX_MISSED_POLLS = 3;

	private Container _playersList;
	private HBoxContainer _playerTemplate;
	private Button _readyButton;
	private Button _startButton;
	private Label _status;
	private Timer _pollTimer;
	private bool _refreshing;
	private int _missedPolls;

	private Guid BattleId => Main3d.Current.Session.BattleId;

	public void OnNavigateTo(NavigateContext context)
	{
		_missedPolls = 0;
		_readyButton.SetPressedNoSignal(false);
		_readyButton.Disabled = false;
		_startButton.Visible = false;
		_startButton.Disabled = false;
		_status.Text = string.Empty;
		GetNode<Label>("MarginContainer/BattleLobby/PlayerHBoxContainer/MyName").Text =
			BattleServer.Current.CurrentProfile?.Name ?? "---";
		ClearPlayers();
		RefreshBattle();
	}

	public override void _Ready()
	{
		_playersList = GetNode<Container>("MarginContainer/BattleLobby/OtherPlayers");
		_playerTemplate = GetNode<HBoxContainer>("MarginContainer/BattleLobby/AnotherPlayer");
		_readyButton = GetNode<Button>("MarginContainer/BattleLobby/PlayerHBoxContainer/btnReady");
		_startButton = GetNode<Button>("MarginContainer/BattleLobby/btnStartGame");
		_status = GetNode<Label>("MarginContainer/BattleLobby/Status");
		_pollTimer = GetNode<Timer>("PollTimer");
		this.VisibilityChanged += () =>
		{
			if (this.Visible)
				_pollTimer.Start();
			else
				_pollTimer.Stop();
		};
	}

	private async void OnReadyToPlay(bool toggled_on)
	{
		_readyButton.Disabled = true;
		await BattleServer.Current.SetPlayerReady(BattleId, toggled_on);
		_readyButton.Disabled = false;
		RefreshBattle();
	}

	private async void RefreshBattle()
	{
		// A slow API shouldn't stack up overlapping polls.
		if (_refreshing || BattleId == Guid.Empty)
			return;

		var battleId = BattleId;
		_refreshing = true;
		var battle = await BattleServer.Current.GetBattle(battleId);
		_refreshing = false;

		// We may have left or moved on while waiting.
		if (!this.Visible || battleId != BattleId)
			return;

		var myId = BattleServer.Current.CurrentProfile?.Id ?? Guid.Empty;
		if (battle == null || battle.Peers.All(p => p.Id != myId))
		{
			if (battle == null && ++_missedPolls < MAX_MISSED_POLLS)
				return;

			BackToLobby("The battle was disbanded.");
			return;
		}
		_missedPolls = 0;

		GetNode<Label>("MarginContainer/BattleLobby/BattleName").Text = battle.Name;

		if (battle.IsRunning)
		{
			// The owner started it; its game server is up.
			Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.PREPAREMENU);
			return;
		}

		ClearPlayers();
		foreach (var p in battle.Peers)
		{
			if (p.Id == myId)
				continue;

			var newp = (HBoxContainer) _playerTemplate.Duplicate();
			newp.Visible = true;
			newp.Name = p.Id.ToString();
			newp.GetNode<Label>("Name").Text = p.Name;
			newp.GetNode<CheckButton>("rdyIndicator").SetPressedNoSignal(p.IsReady);
			_playersList.AddChild(newp);
		}

		// Same rule the API enforces on battle/start.
		_startButton.Visible = battle.Owner == myId && battle.Peers.All(p => p.IsReady);
	}

	private void ClearPlayers()
	{
		foreach (Node child in _playersList.GetChildren())
		{
			_playersList.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void StartTheGame()
	{
		// PrepareForBattle asks the API to start the game server and waits for it.
		_startButton.Disabled = true;
		Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.PREPAREMENU);
	}

	private async void OnLeavePressed()
	{
		await BattleServer.Current.LeaveBattle(BattleId);
		BackToLobby(null);
	}

	private void BackToLobby(string message)
	{
		Main3d.Current.Session.LeaveBattle();
		var context = new NavigateContext();
		if (message != null)
			context.Metadata.Add(LobbyMenu.MESSAGE_META, message);
		Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.LOBBYMENU, context);
	}
}
