using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CommentToGame.DTOs;
using System.Net;
using System.Text.RegularExpressions;
using CommentToGame.Models;   // ← önemli

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

    public async Task<RawgPaged<RawgGameStoreItem>> GetGameStoresAsync(int id)
    {
        var url = $"{_baseUrl}/games/{id}/stores?key={_apiKey}&page_size=40";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RawgPaged<RawgGameStoreItem>>() ?? new();
    }

    public async Task<RawgPagedCreators> GetGameDevelopmentTeamAsync(int id)
    {
        var url = $"{_baseUrl}/games/{id}/development-team?key={_apiKey}&page_size=40";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RawgPagedCreators>() ?? new();
    }

    public async Task<List<StoreLink>> GetStoreLinksAsync(int id, CancellationToken ct = default)
{
    var page = await GetGameStoresAsync(id); // mevcut metodun
    var links = new List<StoreLink>();

    foreach (var s in page?.Results ?? new List<RawgGameStoreItem>())
    {
        var urlStr = s.Url;
        if (string.IsNullOrWhiteSpace(urlStr)) continue;

        var link = new StoreLink
        {
            StoreId = s.Store?.Id ?? s.StoreId,
            Store   = s.Store?.Name,
            Slug    = s.Store?.Slug,
            Domain  = s.Store?.Domain,
            Url     = urlStr
        };

        // Domain yoksa URL'den üret
        if (string.IsNullOrWhiteSpace(link.Domain) && Uri.TryCreate(urlStr, UriKind.Absolute, out var u))
            link.Domain = u.Host;

        // --- Normalize & ExternalId çıkar ---
        // Steam
        var mSteam = Regex.Match(urlStr, @"store\.steampowered\.com/app/(\d+)", RegexOptions.IgnoreCase);
        if (mSteam.Success)
        {
            link.Store = "Steam"; link.Slug = "steam"; link.Domain = "store.steampowered.com";
            link.ExternalId = mSteam.Groups[1].Value;
        }

        // PlayStation (concept id)
        var mPsn = Regex.Match(urlStr, @"store\.playstation\.com/.*/concept/(\d+)", RegexOptions.IgnoreCase);
        if (mPsn.Success)
        {
            link.Store = "PlayStation Store"; link.Slug = "playstation-store"; link.Domain = "store.playstation.com";
            link.ExternalId = mPsn.Groups[1].Value;
        }

        // Xbox yeni mağaza (12 haneli ürün kodu)
        var mXbox = Regex.Match(urlStr, @"xbox\.com/.*/store/.*/([A-Z0-9]{12})", RegexOptions.IgnoreCase);
        if (mXbox.Success)
        {
            link.Store = "Xbox Store"; link.Slug = "xbox-store"; link.Domain = "www.xbox.com";
            link.ExternalId = mXbox.Groups[1].Value;
        }

        // Eski marketplace GUID
        var mXboxOld = Regex.Match(urlStr, @"marketplace\.xbox\.com/.*/Product/.*/([0-9a-f]{8}-[0-9a-f\-]{27})", RegexOptions.IgnoreCase);
        if (mXboxOld.Success)
        {
            link.Store = "Xbox Store"; link.Slug = "xbox-store"; link.Domain = "marketplace.xbox.com";
            link.ExternalId = mXboxOld.Groups[1].Value;
        }

        // Epic
        var mEpic = Regex.Match(urlStr, @"epicgames\.com/.*/store/.*/p/([a-z0-9\-]+)", RegexOptions.IgnoreCase);
        if (mEpic.Success)
        {
            link.Store = "Epic Games Store"; link.Slug = "epic-games"; link.Domain = "store.epicgames.com";
            link.ExternalId = mEpic.Groups[1].Value;
        }

        // GOG
        var mGog = Regex.Match(urlStr, @"gog\.com/game/([a-z0-9\_]+)", RegexOptions.IgnoreCase);
        if (mGog.Success)
        {
            link.Store = "GOG"; link.Slug = "gog"; link.Domain = "www.gog.com";
            link.ExternalId = mGog.Groups[1].Value;
        }

        // Nintendo eShop
        if (link.Domain?.Contains("nintendo.com", StringComparison.OrdinalIgnoreCase) == true)
        {
            link.Store = "Nintendo eShop"; link.Slug = "nintendo-eshop"; link.Domain = "www.nintendo.com";
        }

        links.Add(link);
    }

    // Basit dedupe: ExternalId varsa (Store+ExternalId) benzersiz, yoksa Url’e göre
    return links
      .GroupBy(l => !string.IsNullOrWhiteSpace(l.ExternalId)
                     ? $"{l.Store}|{l.ExternalId}"
                     : $"url|{l.Url}",
               StringComparer.OrdinalIgnoreCase)
      .Select(g => g.First())
      .ToList();
}

    public Task<(int Id, string? Name)> ResolveGameAsync(string slugOrId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<(List<string> screenshots, List<TrailerDto> trailers)> GetMediaAsync(int id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
