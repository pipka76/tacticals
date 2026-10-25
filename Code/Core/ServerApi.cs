using Godot;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using tacticals_api_server.Domain;

/// <summary>
/// HTTP calls from the dedicated game server back to the API (not the player-facing BattleServer).
/// The API passes its URL and this instance's secret token in the environment when it launches us.
/// </summary>
public static class ServerApi
{
	private static readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

	public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	/// <summary>False when the server was started by hand rather than by the API.</summary>
	public static bool IsConfigured =>
		!String.IsNullOrEmpty(OS.GetEnvironment(GameServerExit.ApiUrlEnv))
		&& !String.IsNullOrEmpty(OS.GetEnvironment(GameServerExit.ServerTokenEnv));

	/// <param name="body">Sent as JSON; null sends no body.</param>
	public static Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
	{
		var request = new HttpRequestMessage(method, new Uri(new Uri(OS.GetEnvironment(GameServerExit.ApiUrlEnv)), path));
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", OS.GetEnvironment(GameServerExit.ServerTokenEnv));
		if (body != null)
			request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
		return _http.SendAsync(request);
	}
}
