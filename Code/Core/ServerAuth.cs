using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using tacticals_api_server.Domain;

/// <summary>
/// Dedicated server only (added by Main3d in headless mode). Uses Godot's ENet auth handshake:
/// a connecting client sends the one-time join ticket it got from the API (battle/start or
/// battle/endpoint), we redeem it with the API, and only then complete the handshake. Godot
/// emits PeerConnected only after that, so everything downstream sees verified players only.
/// Also the place to ask which profile an ENet peer is (GetIdentity).
/// </summary>
public partial class ServerAuth : Node
{
	// Leaves room for the API round trip; Godot's default is 3s. Clients use it too (Main3d),
	// since each side drops the connection on its own timer.
	public const double AUTH_TIMEOUT_SEC = 10;

	public static ServerAuth Current { get; internal set; }

	private SceneMultiplayer _multiplayer;
	private readonly Dictionary<long, TicketIdentity> _identities = new();
	private readonly HashSet<long> _verifying = new();

	public override void _Ready()
	{
		Current = this;
		_multiplayer = (SceneMultiplayer)Multiplayer;
		_multiplayer.PeerDisconnected += id => _identities.Remove(id);

		if (!ServerApi.IsConfigured)
		{
			// Started by hand (e.g. local testing): no API to redeem tickets with, accept everyone.
			GD.Print("ServerAuth: not started by the API, accepting all peers");
			return;
		}

		_multiplayer.AuthTimeout = AUTH_TIMEOUT_SEC;
		_multiplayer.AuthCallback = Callable.From<long, byte[]>(OnAuthData);
	}

	/// <summary>Profile behind an ENet peer, or null if unknown (not verified, or no API).</summary>
	public TicketIdentity GetIdentity(long peerId)
	{
		return _identities.GetValueOrDefault(peerId);
	}

	private async void OnAuthData(long peerId, byte[] data)
	{
		// One ticket per connection; a second message mid-verification is ignored.
		if (!_verifying.Add(peerId))
			return;

		var identity = await RedeemAsync(Encoding.UTF8.GetString(data));
		_verifying.Remove(peerId);

		// The peer may have timed out or dropped while we waited on the API.
		if (!_multiplayer.GetAuthenticatingPeers().Contains((int)peerId))
			return;

		if (identity == null)
		{
			GD.Print($"ServerAuth: peer {peerId} rejected (invalid ticket)");
			_multiplayer.DisconnectPeer((int)peerId);
			return;
		}

		_identities[peerId] = identity;
		GD.Print($"ServerAuth: peer {peerId} authenticated as {identity.Name} ({identity.ProfileId})");
		_multiplayer.CompleteAuth((int)peerId);
	}

	private async Task<TicketIdentity> RedeemAsync(string ticket)
	{
		try
		{
			var result = await ServerApi.SendAsync(HttpMethod.Post, $"battle/server/ticket?id={ServerLifecycle.Current.BattleId}", ticket);
			if (result.StatusCode != HttpStatusCode.OK)
				return null;

			return await result.Content.ReadFromJsonAsync<TicketIdentity>(ServerApi.JsonOptions);
		}
		catch (Exception e)
		{
			GD.PrintErr($"ServerAuth: ticket check failed: {e.Message}");
			return null;
		}
	}
}
