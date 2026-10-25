using Godot;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using tacticals_api_server.Domain;

/// <summary>
/// Dedicated server only (added by Main3d in headless mode). Decides when the process quits,
/// since the API treats the process exiting as the end of the battle:
/// battle decided (FinishBattle), every player left, or nobody joined in time.
/// The exit code tells the API which one it was (see GameServerExit).
/// </summary>
public partial class ServerLifecycle : Node
{
	private const int DEFAULT_JOIN_TIMEOUT_SEC = 60;
	private const string BATTLE_ARG = "--battle=";
	private const string JOIN_TIMEOUT_ARG = "--join-timeout=";

	public static ServerLifecycle Current { get; internal set; }

	private Guid _battleId;
	private int _connectedPeers;
	private bool _anyPeerJoined;
	private bool _quitting;

	/// <summary>Battle this server runs, from --battle=; Guid.Empty when started by hand.</summary>
	public Guid BattleId => _battleId;

	public override void _Ready()
	{
		Current = this;

		var args = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();
		Guid.TryParse(ArgValue(args, BATTLE_ARG), out _battleId);
		if (!int.TryParse(ArgValue(args, JOIN_TIMEOUT_ARG), out int joinTimeout))
			joinTimeout = DEFAULT_JOIN_TIMEOUT_SEC;

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		GetTree().CreateTimer(joinTimeout).Timeout += OnJoinTimeout;

		GD.Print($"ServerLifecycle: battle {_battleId}, join timeout {joinTimeout}s");
	}

	/// <summary>
	/// Call when the battle is decided. Reports the results to the API, then quits.
	/// </summary>
	public async void FinishBattle(string statistics)
	{
		if (_quitting)
			return;
		_quitting = true;

		await ReportFinishAsync(statistics);
		Quit(GameServerExit.Finished);
	}

	private void OnPeerConnected(long id)
	{
		_connectedPeers++;
		_anyPeerJoined = true;
		GD.Print($"ServerLifecycle: peer {id} connected ({_connectedPeers} connected)");
	}

	private void OnPeerDisconnected(long id)
	{
		_connectedPeers--;
		GD.Print($"ServerLifecycle: peer {id} disconnected ({_connectedPeers} connected)");

		if (_connectedPeers <= 0 && !_quitting)
		{
			_quitting = true;
			Quit(GameServerExit.Abandoned);
		}
	}

	private void OnJoinTimeout()
	{
		if (_anyPeerJoined || _quitting)
			return;

		_quitting = true;
		Quit(GameServerExit.NoPlayers);
	}

	private async Task ReportFinishAsync(string statistics)
	{
		if (!ServerApi.IsConfigured || _battleId == Guid.Empty)
		{
			GD.PrintErr("ServerLifecycle: not started by the API, skipping finish report");
			return;
		}

		try
		{
			var result = await ServerApi.SendAsync(HttpMethod.Put, $"battle/server/finish?id={_battleId}", statistics ?? string.Empty);
			if (!result.IsSuccessStatusCode)
				GD.PrintErr($"ServerLifecycle: finish report failed: {result.StatusCode}");
		}
		catch (Exception e)
		{
			// The API still cleans up when the process exits; only the results are lost.
			GD.PrintErr($"ServerLifecycle: finish report failed: {e.Message}");
		}
	}

	private void Quit(int exitCode)
	{
		GD.Print($"ServerLifecycle: quitting ({GameServerExit.Describe(exitCode)}, exit code {exitCode})");
		GetTree().Quit(exitCode);
	}

	private static string ArgValue(string[] args, string prefix)
	{
		var arg = args.FirstOrDefault(a => a.StartsWith(prefix));
		return arg?.Substring(prefix.Length);
	}
}
