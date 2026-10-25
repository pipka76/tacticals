using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using tacticals_api_server.Domain;

/// <summary>
/// ENet RPCs between the game server and the players during PrepareForBattle and at its end.
/// Same node path on both sides (Main3d/BattleNetwork), so RPCs reach their counterpart.
///
/// Server (StartHosting, headless): generates the battle's map once and sends it to every
/// verified player as they connect, collects each player's army (SubmitDeploy), and tells
/// everyone to begin once every player has deployed.
/// Client: receives the map and progress (events for PrepareForBattle), then builds the level
/// on BeginBattle.
/// </summary>
public partial class BattleNetwork : Node
{
	private const string MAP_SCENE = "res://Scenes/Maps/Plains.tscn";
	private const int MAP_SIZE = 100;

	public static BattleNetwork Current { get; internal set; }

	/// <summary>Client: the map arrived from the game server.</summary>
	public event Action<MapBlock[][]> MapReceived;
	/// <summary>Client: how many players have deployed, out of how many.</summary>
	public event Action<int, int> DeployProgressChanged;

	// Client: the map we got from the game server, built into the level on BeginBattle.
	private MapBlock[][] _map;

	// Server state.
	private byte[] _packedMap;
	// Profiles in the battle, from the API; null when started by hand (wait for whoever is connected).
	private HashSet<Guid> _expectedPlayers;
	// Profile behind each connected peer. Kept here because ServerAuth may forget a peer
	// before our PeerDisconnected handler runs. Missing when started by hand.
	private readonly Dictionary<long, Guid> _peerProfiles = new();
	// Deployed armies by ENet peer; a peer that disconnects is dropped from it.
	private readonly Dictionary<long, ArmySetup> _deployed = new();
	private bool _battleBegun;

	public override void _Ready()
	{
		Current = this;
	}

	/// <summary>Dedicated server only (Main3d, before the ENet server starts).</summary>
	public void StartHosting()
	{
		var map = new MapGenerator(MAP_SIZE, MAP_SIZE).GenerateMap(new FlowFieldManager());
		_packedMap = MapGenerator.Pack(map);
		GD.Print($"BattleNetwork: map generated ({_packedMap.Length / 1024} KiB packed)");

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		LoadExpectedPlayers();
	}

	private async void LoadExpectedPlayers()
	{
		var battleId = ServerLifecycle.Current?.BattleId ?? Guid.Empty;
		if (!ServerApi.IsConfigured || battleId == Guid.Empty)
			return;

		try
		{
			var result = await ServerApi.SendAsync(HttpMethod.Get, $"battle/get?id={battleId}", null);
			if (result.StatusCode != HttpStatusCode.OK)
			{
				GD.PrintErr($"BattleNetwork: battle/get failed: {result.StatusCode}");
				return;
			}

			var battle = await result.Content.ReadFromJsonAsync<Battle>(ServerApi.JsonOptions);
			_expectedPlayers = battle?.Peers.Select(p => p.Id).ToHashSet();
			GD.Print($"BattleNetwork: expecting {_expectedPlayers?.Count} players");
			CheckAllDeployed();
		}
		catch (Exception e)
		{
			// Fall back to waiting for whoever is connected.
			GD.PrintErr($"BattleNetwork: battle/get failed: {e.Message}");
		}
	}

	private void OnPeerConnected(long peerId)
	{
		var identity = ServerAuth.Current?.GetIdentity(peerId);
		if (identity != null)
			_peerProfiles[peerId] = identity.ProfileId;

		RpcId(peerId, nameof(ReceiveMap), _packedMap);

		// A player reconnecting after the start goes straight into the battle.
		if (_battleBegun)
			RpcId(peerId, nameof(BeginBattle));
		else
			BroadcastProgress();
	}

	private void OnPeerDisconnected(long peerId)
	{
		// A player who left during PrepareForBattle no longer holds the others up.
		if (_peerProfiles.Remove(peerId, out var profileId) && !_battleBegun)
			_expectedPlayers?.Remove(profileId);

		_deployed.Remove(peerId);
		if (!_battleBegun)
		{
			BroadcastProgress();
			CheckAllDeployed();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void SubmitDeploy(string armySetup)
	{
		if (!Multiplayer.IsServer() || _battleBegun)
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		var army = JsonSerializer.Deserialize<ArmySetup>(armySetup);
		_deployed[peerId] = army;
		var name = ServerAuth.Current?.GetIdentity(peerId)?.Name ?? peerId.ToString();
		GD.Print($"BattleNetwork: {name} deployed {armySetup}");

		BroadcastProgress();
		CheckAllDeployed();
	}

	private (int Deployed, int Expected) GetProgress()
	{
		var connected = Multiplayer.GetPeers();
		if (_expectedPlayers == null)
			return (_deployed.Count, connected.Length);

		var deployedProfiles = _deployed.Keys
			.Where(_peerProfiles.ContainsKey)
			.Select(p => _peerProfiles[p])
			.Where(_expectedPlayers.Contains)
			.Distinct()
			.Count();
		return (deployedProfiles, _expectedPlayers.Count);
	}

	private void BroadcastProgress()
	{
		var (deployed, expected) = GetProgress();
		Rpc(nameof(DeployProgress), deployed, expected);
	}

	private void CheckAllDeployed()
	{
		var (deployed, expected) = GetProgress();
		if (_battleBegun || expected == 0 || deployed < expected)
			return;

		_battleBegun = true;
		GD.Print($"BattleNetwork: all {expected} players deployed, beginning the battle");
		Rpc(nameof(BeginBattle));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ReceiveMap(byte[] packedMap)
	{
		_map = MapGenerator.Unpack(packedMap);
		GD.Print($"BattleNetwork: map received ({packedMap.Length / 1024} KiB packed)");
		MapReceived?.Invoke(_map);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void DeployProgress(int deployed, int expected)
	{
		DeployProgressChanged?.Invoke(deployed, expected);
	}

	/// <summary>Client: sends our army to the game server; the battle begins once everyone has.</summary>
	public void Deploy(ArmySetup army)
	{
		RpcId(1, nameof(SubmitDeploy), JsonSerializer.Serialize(army));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void BeginBattle()
	{
		if (_map == null)
		{
			GD.PrintErr("BattleNetwork: BeginBattle before the map arrived");
			return;
		}

		Main3d.Current.HideMenus();
		var level = Main3d.Current.StartGame(MAP_SCENE, _map);
		var player = level.SpawnPlayer(new Vector2(0, 0));
		// Units aren't replicated yet: keep the local player's input to ourselves rather than
		// syncing it to a server that has no matching node.
		player.GetNode<MultiplayerSynchronizer>("PlayerInput").PublicVisibility = false;
	}
}
