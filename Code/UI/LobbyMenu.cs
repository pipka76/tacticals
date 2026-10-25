using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Step 2 of the battle flow: lists the battles on the API that haven't started yet, and
/// creates or joins one. The lobby lives on the API; we connect to a game server only in PrepareForBattle.
/// </summary>
public partial class LobbyMenu : Control, IGameMenu
{
	public const string MESSAGE_META = "message";

	private Timer _refreshTimer;
	private Label _error;
	private bool _refreshing;

	public NavigateContext Context { get; set; }

	public override void _Ready()
	{
		_refreshTimer = GetNode<Timer>("RefreshTimer");
		_error = GetNode<Label>("MarginContainer/CreateJoinBattle/Error");
		this.VisibilityChanged += OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (this.Visible)
		{
			_refreshTimer.Start();
			RefreshBattles();
		}
		else
			_refreshTimer.Stop();
	}

	public async void RefreshBattles()
	{
		// A slow API shouldn't stack up overlapping refreshes.
		if (_refreshing)
			return;

		_refreshing = true;
		var list = await BattleServer.Current.ListAllBattles();
		_refreshing = false;

		if (this.Visible)
			OnGetRegisteredBattles(list);
	}

	private void OnGetRegisteredBattles(IEnumerable<tacticals_api_server.Domain.Battle> battles)
	{
		var battlesRoot = GetNode<Container>("MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/ExistingGames");
		var battleTemplate = GetNode<Control>("MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/hostedGameTemplate");

		foreach (Node child in battlesRoot.GetChildren())
		{
			battlesRoot.RemoveChild(child);
			child.QueueFree();
		}
		
		foreach (var b in battles)
		{
			var bi = (Button)battleTemplate.Duplicate();
			bi.Visible = true;
			bi.Text = $"{b.Name} ({b.Peers?.Count ?? 0}/4)";
			var battleId = b.ID;
			bi.Pressed += () => BattleSelected(battleId);
			battlesRoot.AddChild(bi);
		}
	}

	private async void BattleSelected(Guid id)
	{
		GD.Print($"BattleSelected ID: {id}");
		
		if (await BattleServer.Current.JoinBattle(id))
		{
			GD.Print($"Battle joined: {id}");
			EnterBattle(id, false);
		}
		else
			_error.Text = "Could not join the battle (it is full or already started).";
	}
	
	private void CreateNewBattleStep1()
	{
		GetNode<HBoxContainer>(
			"MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/hBoxBattleName").Visible = true;
	}

	private async void CreateNewBattleFinal()
	{
		var battleName = GetNode<TextEdit>(
			"MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/hBoxBattleName/TextEdit");
		if (String.IsNullOrWhiteSpace(battleName.Text))
		{
			_error.Text = "Enter a battle name.";
			return;
		}
		
		// We own the new battle and are already its first player.
		var battleId = await BattleServer.Current.RegisterNewBattle(battleName.Text.Trim());
		
		if (battleId != Guid.Empty)
		{
			GD.Print($"New battle registered: {battleId}");
			EnterBattle(battleId, true);
		}
		else
			_error.Text = "Could not create the battle (the name may be taken).";
	}

	private void EnterBattle(Guid battleId, bool isOwner)
	{
		var session = Main3d.Current.Session;
		session.BattleId = battleId;
		session.IsOwner = isOwner;
		Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.BATTLEMENU);
	}

	private void OnBackPressed()
	{
		Main3d.Current.NavigateTo(Main3d.NAVIGATE_TARGET.STARTMENU);
	}

	private void CreateNewDemoBattle()
	{
        this.Visible = false;
        var map = Main3d.Current.StartGame("res://Scenes/Maps/Plains.tscn");
        map.SpawnPlayer(new Vector2(0,0));
	}

	private void CreateNewCampainDemoBattle()
	{
		this.Visible = false;
		var map = Main3d.Current.StartGame("res://Scenes/Maps/CampainDemo.tscn");
		map.SpawnPlayer(new Vector2(0,0));
	}

	public void OnNavigateTo(NavigateContext context)
	{
		// e.g. why we were sent back here from a battle.
		_error.Text = context != null && context.Metadata.TryGetValue(MESSAGE_META, out var message) ? message : string.Empty;
	}
}
