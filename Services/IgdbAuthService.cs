using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CommentToGame.Services;
    /// <summary>
    /// Twitch OAuth (Client Credentials) ile IGDB token alan basit servis.
    /// </summary>
    public sealed class IgdbAuthService
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        private string? _token;
        private DateTime _expiresAtUtc;

        public IgdbAuthService(IHttpClientFactory http, IConfiguration cfg)
        { _http = http; _cfg = cfg; }

        public async Task<string> GetTokenAsync(CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(_token) && DateTime.UtcNow < _expiresAtUtc.AddMinutes(-2))
                return _token!;

            var clientId = _cfg["Igdb:ClientId"] ?? throw new InvalidOperationException("Igdb:ClientId missing");
            var clientSecret = _cfg["Igdb:ClientSecret"] ?? throw new InvalidOperationException("Igdb:ClientSecret missing");

            var http = _http.CreateClient();
            var url = $"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials";
            using var resp = await http.PostAsync(url, content: null, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            _token = doc.RootElement.GetProperty("access_token").GetString();
            var seconds = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(seconds);
            return _token!;
        }

        public async Task AddAuthAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var token = await GetTokenAsync(ct);
            var clientId = _cfg["Igdb:ClientId"]!;
            req.Headers.Add("Client-ID", clientId);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
