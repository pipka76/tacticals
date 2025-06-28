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

public partial class BattleServer : Node
{
	private const string ServerApiUrl = "http://172.33.1.169:5000/";
	private System.Net.Http.HttpClient _serverApi;
	private string _currentToken;

	public struct NewBattle
	{
		public Guid id { set; get; }
		public int port { set; get; }
	}

	public static BattleServer Current { get; internal set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_currentToken = null;
		_serverApi = new System.Net.Http.HttpClient();
		_serverApi.BaseAddress = new Uri(ServerApiUrl);
		Current = this;
	}
	
	public async Task<NewBattle> RegisterNewBattle(string name)
	{
		try
		{
			var result = await _serverApi.PutAsync($"/register?name={name}", null);
			if (result.StatusCode != HttpStatusCode.OK)
				return new NewBattle() { id = Guid.Empty, port = -1 };
			
			return await result.Content.ReadFromJsonAsync<NewBattle>();
		}
		catch (Exception e)
		{
			return new NewBattle() { id = Guid.Empty, port = -1 };
		}
	}

	public async Task<bool> JoinBattle(Guid id, int peerId)
	{
		try
		{
			var result = await _serverApi.PutAsync($"/join?id={id.ToString()}&peerId={peerId}", null);
			if (result.StatusCode != HttpStatusCode.OK)
				return false;
			
			return true;
		}
		catch (Exception e)
		{
			return false;
		}
	}
	
	public async Task<bool> AuthorizeBattle(Guid id, int peerId)
	{
		try
		{
			var result = await _serverApi.PutAsync($"/authorize?id={id.ToString()}&ownerPeer={peerId}", null);
			if (result.StatusCode != HttpStatusCode.OK)
				return false;
			
			return true;
		}
		catch (Exception e)
		{
			return false;
		}
	}

	public async Task SetPlayerReady(Guid id, int peerId, bool isReady, string name)
	{
		try
		{
			await _serverApi.PutAsync($"/playerready?id={id.ToString()}&peerId={peerId}&isReady={isReady}&name={HttpUtility.UrlEncode(name)}", null);
		}
		catch (Exception e)
		{
			GD.Print($"SetPlayerReady: {e.Message}");
		}
	}

	public async Task StartBattle(Guid id, int peerId)
	{
		try
		{
			await _serverApi.PutAsync($"/start?id={id.ToString()}&peerId={peerId}", null);
		}
		catch (Exception e)
		{
			GD.Print($"StartBattle: {e.Message}");
		}
	}
	
	public async Task<tacticals_api_server.Domain.Battle> GetBattle(Guid id)
	{
		try
		{
			return await _serverApi.GetFromJsonAsync<tacticals_api_server.Domain.Battle>($"/get?id={id.ToString()}");
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
			return await _serverApi.GetFromJsonAsync<IEnumerable<tacticals_api_server.Domain.Battle>>($"/list");
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
				_currentToken = null;
				GD.Print($"Login failed: {result.StatusCode}");
				return false;
			}

			_currentToken = await result.Content.ReadAsStringAsync();
			return true;
		}
		catch (Exception e)
		{
			_currentToken = null;
			GD.Print($"LoginProfile exception: {e.Message}");
			return false;
		}
	}

	public void Logout()
	{
		_currentToken = null;
	}

	public async Task<UProfile> GetProfile()
	{
		try
		{
			_serverApi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken);
			var result = await _serverApi.GetAsync("/profile/get");
			if (!result.IsSuccessStatusCode)
			{
				GD.Print($"GetProfile failed: {result.StatusCode}");
				return null;
			}

			return await result.Content.ReadFromJsonAsync<UProfile>();
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
