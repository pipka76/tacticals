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

		// Peers are identified by the profile behind their join ticket (see ServerAuth).
		if (ServerAuth.Current?.GetIdentity(callerId)?.ProfileId != battleServer.Owner)
		{
			GD.Print($"OnBattleStart called by unauthorized peer {callerId}");
			return;
		}

		_currentBattle = battle;
		// Clear any peers that are not ready and sync with server state
		_currentBattle.Peers.Clear();
		_currentBattle.Peers.AddRange(battleServer.Peers.Where(p => p.IsReady));

		var mm = new MapGenerator(100, 100);
		var map = mm.GenerateMap(null);
		string mapData = mm.ToJson(map);
		foreach (var peerId in Multiplayer.GetPeers())
		{
			var identity = ServerAuth.Current.GetIdentity(peerId);
			if (identity != null && _currentBattle.Peers.Any(p => p.Id == identity.ProfileId))
				GetParent().GetNode("BattleMenu").RpcId(peerId, "OnStartBattle", _currentBattle.MapName, mapData);
		}
	}
}
