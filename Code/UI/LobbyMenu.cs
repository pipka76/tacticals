using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

public partial class LobbyMenu : Control, IGameMenu
{
	private System.Timers.Timer _refreshTimer;
	
	private class BattleInfo
	{
		public Guid id { get; set; }
		public string name { get; set; }
		public int count { get; set; }
	}

	public NavigateContext Context { get; set; }

	public override void _Ready()
	{
		_refreshTimer = new System.Timers.Timer();
		_refreshTimer.Enabled = false;
		_refreshTimer.Interval = 2000;
		_refreshTimer.Elapsed += RefreshTimerOnElapsed;
		this.VisibilityChanged += OnVisibilityChanged;
	}

	private void RefreshTimerOnElapsed(object sender, ElapsedEventArgs e)
	{
		CallDeferred("RefreshBattles");
	}

	private void OnVisibilityChanged()
	{
		_refreshTimer.Enabled = this.Visible;
	}

	public void RefreshBattles()
	{
		var list = Task.Run(async () => await BattleServer.Current.ListAllBattles()).Result;
		OnGetRegisteredBattles(list);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void OnGetRegisteredBattles(IEnumerable<tacticals_api_server.Domain.Battle> battles)
	{
		var battlesRoot = GetNode<Container>("MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/ExistingGames");
		var battleTemplate = GetNode<Control>("MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/hostedGameTemplate");

		while(battlesRoot.GetChildCount() > 0)
		{
			battlesRoot.RemoveChild(battlesRoot.GetChild(0));
		}
		
		foreach (var b in battles)
		{
			var bi = (Button)battleTemplate.Duplicate();
			bi.Visible = true;
			bi.Text = b.Name;
			bi.Pressed += () => BattleSelected(bi);
			bi.SetMeta("battleid", b.ID.ToString());
			bi.SetMeta("serverport", b.Port);
			battlesRoot.AddChild(bi);
		}
	}

	private void BattleSelected(Button button)
	{
		string id = button.GetMeta("battleid").AsString();
		int port = button.GetMeta("serverport").AsInt32();
		GD.Print("BattleSelected ID: " + id);
		
		((Main)GetTree().CurrentScene).JoinServer(port);
		int peerId = Multiplayer.GetUniqueId();
		var result = Task.Run(async () => await BattleServer.Current.JoinBattle(Guid.Parse(id), peerId)).Result;
		if (result)
		{
			GD.Print("Battle joined: " + id);

			var context = new NavigateContext();
			context.Command = "JoinExisting";
			context.Metadata.Add("battleid", id);
			((Main)GetTree().CurrentScene).NavigateTo(Main.NAVIGATE_TARGET.BATTLEMENU, context);
		}
	}
	
	private void CreateNewBattleStep1()
	{
		GetNode<HBoxContainer>(
			"MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/hBoxBattleName").Visible = true;
	}

	private void CreateNewBattleFinal()
	{
		var battleName = GetNode<TextEdit>(
			"MarginContainer/CreateJoinBattle/HBoxContainer/ScrollContainer/VBoxContainer/hBoxBattleName/TextEdit");
		
		var result =  Task.Run(async () => await BattleServer.Current.RegisterNewBattle(battleName.Text)).Result;
		
		if (result.id != Guid.Empty)
		{
			GD.Print($"New server created on port {result.port}: {result.id})");
			GD.Print($"Connecting to the server ...");
			((Main)GetTree().CurrentScene).JoinServer(result.port);
			GD.Print($"Authorizing the server as mine ...");
			int peerId = Multiplayer.GetUniqueId();
			var authorized =  Task.Run(async () => await BattleServer.Current.AuthorizeBattle(result.id, peerId)).Result;
			if (authorized)
			{
				GD.Print($"Authorized");
				var context = new NavigateContext();
				context.Command = "CreateNew";
				context.Metadata.Add("battleid", result.id.ToString());
				((Main)GetTree().CurrentScene).NavigateTo(Main.NAVIGATE_TARGET.BATTLEMENU, context);
			}
			else
			{
				GD.Print($"Failed to authorize!");
			}
		}
	}

	public void OnNavigateTo(NavigateContext context)
	{
	}
}
