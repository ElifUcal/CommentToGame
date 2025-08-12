using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CommentToGame.DTOs;
using System.Net;   // ← önemli

namespace CommentToGame.Services;

public class RawgClient : IRawgClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public RawgClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = Environment.GetEnvironmentVariable("RAWG_KEY")
                  ?? config["Rawg:Key"]
                  ?? throw new InvalidOperationException("RAWG API key not set.");
        _baseUrl = config["Rawg:BaseUrl"] ?? "https://api.rawg.io/api";
    }

    public async Task<RawgPaged<RawgGameSummary>> GetGamesAsync(int page = 1, int pageSize = 40)
    {
        var url = $"{_baseUrl}/games?key={_apiKey}&page={page}&page_size={pageSize}";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RawgPaged<RawgGameSummary>>() ?? new();
    }

    public async Task<RawgGameDetail?> GetGameDetailAsync(int id)
    {
        var url = $"{_baseUrl}/games/{id}?key={_apiKey}";
        using var resp = await _http.GetAsync(url);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null; // 404'leri yumuşakça geç

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RawgGameDetail>();
    }

    public async Task<RawgPaged<RawgGameSummary>> SearchGamesAsync(string query, int page = 1, int pageSize = 40)
    {
        var url = $"{_baseUrl}/games?key={_apiKey}&search={Uri.EscapeDataString(query)}&page={page}&page_size={pageSize}";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RawgPaged<RawgGameSummary>>() ?? new();
    }

    // RawgClient.cs
    public async Task<RawgPaged<RawgGameSummary>> GetGameSeriesAsync(int id)
    {
        var url = $"{_baseUrl}/games/{id}/game-series?key={_apiKey}&page_size=40";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RawgPaged<RawgGameSummary>>() ?? new();
    }

    public async Task<RawgPaged<RawgGameSummary>> GetGameAdditionsAsync(int id)
{
    var url = $"{_baseUrl}/games/{id}/additions?key={_apiKey}&page_size=40";
    using var resp = await _http.GetAsync(url);
    resp.EnsureSuccessStatusCode();
    return await resp.Content.ReadFromJsonAsync<RawgPaged<RawgGameSummary>>() ?? new();
}


}
