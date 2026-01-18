using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattleNetwork : Node
{
	private tacticals_api_server.Domain.Battle _currentBattle;
	public static BattleNetwork Current { get; internal set; }

	public override void _Ready()
	{
		Current = this;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void OnBattleStart(tacticals_api_server.Domain.Battle battle)
	{
		if(battle == null)
			return;
		
		int callerId = Multiplayer.GetRemoteSenderId();
		GD.Print($"OnBattleStart called by peer {callerId}");
		
		var battleServer = Task.Run(async () => await BattleServer.Current.GetBattle(battle.ID)).Result;
		if (battleServer == null)
			return;

		if (battleServer.Owner != callerId)
		{
			GD.Print($"OnBattleStart called by unauthorized peer {callerId}");
			return;
		}

		Task.Run(async () => await BattleServer.Current.StartBattle(battle.ID, callerId)).Wait();

		_currentBattle = battle;
		// Clear any peers that are not ready and sync with server state
		_currentBattle.Peers.Clear();
		_currentBattle.Peers.AddRange(battleServer.Peers.Where(p => p.IsReady));

		var mm = new MapGenerator(100, 100);
		var map = mm.GenerateMap();
		string mapData = mm.ToJson(map);
		foreach (var peer in _currentBattle.Peers)
		{
			GetParent().GetNode("BattleMenu").RpcId(peer.Id, "OnStartBattle", _currentBattle.MapName, mapData);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void OnPlayerReady(string id, int peerId, string name)
	{
		var battle = Task.Run(async () =>
		{
			await BattleServer.Current.SetPlayerReady(Guid.Parse(id), peerId, true, name);
			return await BattleServer.Current.GetBattle(Guid.Parse(id));
		}).Result;

		if (battle != null)
		{
			foreach (var peer in battle.Peers)
			{
				if(peer.Id != peerId)
					GetParent().GetNode("BattleMenu").RpcId(peer.Id, "OnOtherPlayerReady");
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void OnPlayerNotReady(string id, int peerId, string name)
	{
		var battle = Task.Run(async () =>
		{
			await BattleServer.Current.SetPlayerReady(Guid.Parse(id), peerId, false, name);
			return await BattleServer.Current.GetBattle(Guid.Parse(id));
		}).Result;

		if (battle != null)
		{
			foreach (var peer in battle.Peers)
			{
				if(peer.Id != peerId)
					GetParent().GetNode("BattleMenu").RpcId(peer.Id, "OnOtherPlayerNotReady");
			}
		}
	}
}
