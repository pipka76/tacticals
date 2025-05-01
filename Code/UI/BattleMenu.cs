using Godot;
using System;
using System.Threading.Tasks;

public partial class BattleMenu : Control, IGameMenu
{
	private Container _playersList;
	private HBoxContainer _playerTemplate;
	private Guid _battleId;
	
	public void OnNavigateTo(NavigateContext context)
	{
		if (context == null)
			return;

		var bName = GetNode<Label>("MarginContainer/BattleLobby/BattleName");
		string battleId = context.Metadata["battleid"];
		GD.Print($"Getting battle details {battleId} ...");
		var b = Task.Run(async () => await BattleServer.Current.GetBattle(Guid.Parse(battleId))).Result;
		if(b == null)
			return;
		GD.Print($"Battle details read: {b.Name}");
		bName.Text = b.Name;
		_battleId = b.ID;
	}
	
	public override void _Ready()
	{
		_playersList = GetNode<Container>("MarginContainer/BattleLobby/OtherPlayers");
		_playerTemplate = GetNode<HBoxContainer>("MarginContainer/BattleLobby/AnotherPlayer");
	}

	private void OnReadyToPlay(bool toggled_on)
	{
		var name = GetNode<TextEdit>("MarginContainer/BattleLobby/PlayerHBoxContainer/TextEdit");
		if (toggled_on)
		{
			name.Editable = false;
			BattleNetwork.Current.RpcId(1, "OnPlayerReady", _battleId.ToString(), Multiplayer.GetUniqueId(), name.Text);
		}
		else
		{
			name.Editable = true;
			BattleNetwork.Current.RpcId(1, "OnPlayerNotReady", _battleId.ToString(), Multiplayer.GetUniqueId(), name.Text);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void OnOtherPlayerReady()
	{
		int rdyCount = 0;
		var battle = Task.Run(async () => await BattleServer.Current.GetBattle(_battleId)).Result;
		if (battle != null)
		{
			while(_playersList.GetChildCount() > 0)
				_playersList.RemoveChild(_playersList.GetChild(0));

			foreach (var p in battle.Peers)
			{
				if (p.Id == Multiplayer.GetUniqueId())
				{
					if (p.IsReady) rdyCount++;
					continue;
				}
				

				var newp = (HBoxContainer) _playerTemplate.Duplicate();
				newp.Visible = true;
				newp.Name = p.Id.ToString();
				var namep = newp.GetNode<Label>("Name");
				namep.Text = p.Name;
				var indi = newp.GetNode<CheckButton>("MarginContainer/BattleLobby/AnotherPlayer/rdyIndicator");
				indi.SetPressed(p.IsReady);
				_playersList.AddChild(newp);

				if (p.IsReady) rdyCount++;
			}
			
			if(rdyCount >= 2 && battle.Owner == Multiplayer.GetUniqueId())
				OnAllPlayersReady();
			else
				OnAllPlayersNotReady();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void OnOtherPlayerNotReady()
	{
		OnOtherPlayerReady();
	}

	private void OnAllPlayersReady()
	{
		GD.Print("OnAllPlayersReady");
		GetNode<Button>("MarginContainer/BattleLobby/btnStartGame").Visible = true;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void OnAllPlayersNotReady()
	{
		GetNode<Button>("MarginContainer/BattleLobby/btnStartGame").Visible = false;
	}

	private void StartTheGame()
	{
		BattleServer.Current.RpcId(1, "StartBattle", Multiplayer.GetUniqueId());
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void OnStartBattle(string mapName, string mapData)
	{
		this.Visible = false;
		var map = (Plains) GD.Load<PackedScene>(mapName).Instantiate();
		map.ImportLevelData(mapData);
		GetTree().CurrentScene.GetNode<Node>("Map").AddChild(map);

		var player = (Player)GD.Load<PackedScene>("res://Scenes/Game/Player.tscn").Instantiate();
		GetTree().CurrentScene.GetNode<Node>("Map").GetChild(0).GetNode<Node>("Players").AddChild(player);
	}
}
