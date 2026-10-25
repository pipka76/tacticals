using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using tacticals_api_server.Domain;

/// <summary>
/// Player-side client for the REST API. Battle calls are authenticated with the login token,
/// which is attached to every request after LoginProfile; the API identifies us by profile.
/// </summary>
public partial class BattleServer : Node
{
	private const string ServerApiUrl = "http://172.33.1.169:5000/";
	private System.Net.Http.HttpClient _serverApi;
	private string _currentToken;

	private struct NewBattle
	{
		public Guid id { set; get; }
	}

	public static BattleServer Current { get; internal set; }

	/// <summary>Logged-in profile (set by GetProfile), or null.</summary>
	public UProfile CurrentProfile { get; private set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_currentToken = null;
		_serverApi = new System.Net.Http.HttpClient();
		_serverApi.BaseAddress = new Uri(ServerApiUrl);
		Current = this;
	}

	/// <summary>Creates a battle owned by us (we're its first player); Guid.Empty on failure.</summary>
	public async Task<Guid> RegisterNewBattle(string name)
	{
		try
		{
			var result = await _serverApi.PutAsync($"battle/register?name={HttpUtility.UrlEncode(name)}", null);
			if (result.StatusCode != HttpStatusCode.OK)
				return Guid.Empty;

			return (await result.Content.ReadFromJsonAsync<NewBattle>()).id;
		}
		catch (Exception e)
		{
			GD.Print($"RegisterNewBattle: {e.Message}");
			return Guid.Empty;
		}
	}

	public async Task<bool> JoinBattle(Guid id)
	{
		try
		{
			var result = await _serverApi.PutAsync($"battle/join?id={id}", null);
			return result.StatusCode == HttpStatusCode.OK;
		}
		catch (Exception e)
		{
			GD.Print($"JoinBattle: {e.Message}");
			return false;
		}
	}

	/// <summary>Leaves a battle that hasn't started; if we own it, it's disbanded.</summary>
	public async Task<bool> LeaveBattle(Guid id)
	{
		try
		{
			var result = await _serverApi.PutAsync($"battle/leave?id={id}", null);
			return result.StatusCode == HttpStatusCode.OK;
		}
		catch (Exception e)
		{
			GD.Print($"LeaveBattle: {e.Message}");
			return false;
		}
	}

	public async Task SetPlayerReady(Guid id, bool isReady)
	{
		try
		{
			await _serverApi.PutAsync($"battle/playerready?id={id}&isReady={isReady}", null);
		}
		catch (Exception e)
		{
			GD.Print($"SetPlayerReady: {e.Message}");
		}
	}

	/// <summary>
	/// Owner only, once everyone is ready: launches the game server. Returns where to connect
	/// plus our join ticket, or null on failure.
	/// </summary>
	public async Task<BattleEndpoint> StartBattle(Guid id)
	{
		try
		{
			var result = await _serverApi.PutAsync($"battle/start?id={id}", null);
			if (result.StatusCode != HttpStatusCode.OK)
			{
				GD.Print($"StartBattle failed: {result.StatusCode}");
				return null;
			}

			return await result.Content.ReadFromJsonAsync<BattleEndpoint>();
		}
		catch (Exception e)
		{
			GD.Print($"StartBattle: {e.Message}");
			return null;
		}
	}

	/// <summary>Where to connect for a started battle, with a fresh join ticket; null until started.</summary>
	public async Task<BattleEndpoint> GetEndpoint(Guid id)
	{
		try
		{
			var result = await _serverApi.GetAsync($"battle/endpoint?id={id}");
			if (result.StatusCode != HttpStatusCode.OK)
				return null;

			return await result.Content.ReadFromJsonAsync<BattleEndpoint>();
		}
		catch (Exception e)
		{
			GD.Print($"GetEndpoint: {e.Message}");
			return null;
		}
	}

	public async Task<tacticals_api_server.Domain.Battle> GetBattle(Guid id)
	{
		try
		{
			return await _serverApi.GetFromJsonAsync<tacticals_api_server.Domain.Battle>($"battle/get?id={id}");
		}
		catch (Exception e)
		{
			return null;
		}
	}

	public async Task<IEnumerable<tacticals_api_server.Domain.Battle>> ListAllBattles()
	{
		try
		{
			return await _serverApi.GetFromJsonAsync<IEnumerable<tacticals_api_server.Domain.Battle>>("battle/list");
		}
		catch (Exception e)
		{
			return new List<tacticals_api_server.Domain.Battle>();
		}
	}

	public async Task<bool> LoginProfile(UProfile profile)
	{
		try
		{
			// Send password hash as raw JSON string (with quotes)
			var content = new StringContent($"\"{profile.PasswordHash}\"", Encoding.UTF8, "text/json");
			var result = await _serverApi.PostAsync(
				$"/profile/login/{HttpUtility.UrlEncode(profile.Email)}",
				content);
			if (!result.IsSuccessStatusCode)
			{
				Logout();
				GD.Print($"Login failed: {result.StatusCode}");
				return false;
			}

			_currentToken = await result.Content.ReadAsStringAsync();
			_serverApi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken);
			return true;
		}
		catch (Exception e)
		{
			Logout();
			GD.Print($"LoginProfile exception: {e.Message}");
			return false;
		}
	}

	public void Logout()
	{
		_currentToken = null;
		CurrentProfile = null;
		_serverApi.DefaultRequestHeaders.Authorization = null;
	}

	public async Task<UProfile> GetProfile()
	{
		try
		{
			var result = await _serverApi.GetAsync("/profile/get");
			if (!result.IsSuccessStatusCode)
			{
				GD.Print($"GetProfile failed: {result.StatusCode}");
				return null;
			}

			CurrentProfile = await result.Content.ReadFromJsonAsync<UProfile>();
			return CurrentProfile;
		}
		catch (Exception e)
		{
			GD.Print($"GetProfile exception: {e.Message}");
			return null;
		}
	}

	public async Task<bool> RegisterProfile(UProfile profile)
	{
		var result = await _serverApi.PostAsJsonAsync("/profile/create", profile);
		if (!result.IsSuccessStatusCode)
		{
			GD.Print($"RegisterProfile failed: {result.StatusCode}");
			return false;
		}

		return true;
	}
}
