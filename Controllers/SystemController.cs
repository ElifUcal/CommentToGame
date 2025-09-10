using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.Models;
using CommentToGame.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using System.Net.Http;

namespace CommentToGame.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly IMongoCollection<Game> _games;
        private readonly IConfiguration _config;
        private readonly ISystemLogger _logger;
        private readonly IHttpClientFactory _http;

        public SystemController(
            MongoDbService service,
            IConfiguration config,
            ISystemLogger logger,
            IHttpClientFactory http)
        {
            var db = service?.Database
                ?? throw new InvalidOperationException("MongoDbService.database is null.");

            _games = db.GetCollection<Game>("Games");
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        private static (HealthState state, string color) MapState(HealthState s) => s switch
        {
            HealthState.Healthy  => (s, "#22C55E"),
            HealthState.Degraded => (s, "#F59E0B"),
            _                    => (HealthState.Down, "#EF4444")
        };

        private static HealthItemDto Make(string title, HealthState s, long? ms = null, string? detail = null)
        {
            var (state, color) = MapState(s);
            return new HealthItemDto { Title = title, Status = state, Color = color, LatencyMs = ms, Detail = detail };
        }

        private async Task<(HealthState, long?, string?)> PingMongoAsync(CancellationToken ct)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var db = _games.Database;
                await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
                sw.Stop();
                return (HealthState.Healthy, sw.ElapsedMilliseconds, null);
            }
            catch (Exception ex)
            {
                return (HealthState.Down, null, ex.Message);
            }
        }

        private static bool IsReachable(HttpResponseMessage res)
        {
            var code = (int)res.StatusCode;
            if (code is >= 200 and < 400) return true;  // 2xx-3xx
            // Yetkisiz / rate-limit ama servis ayakta → reachable say
            if (code is 401 or 403 or 429) return true;
            return false;
        }

        private async Task<(HealthState, long?, string?)> HeadAsync(string? url, int timeoutMs = 2500)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (HealthState.Degraded, null, "URL not configured");

            try
            {
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

                var sw = Stopwatch.StartNew();
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();

                return (IsReachable(res) ? HealthState.Healthy : HealthState.Degraded,
                        sw.ElapsedMilliseconds,
                        $"HTTP {(int)res.StatusCode}");
            }
            catch (TaskCanceledException) { return (HealthState.Degraded, null, "Timeout"); }
            catch (Exception ex)          { return (HealthState.Down, null, ex.Message); }
        }

        private async Task<(HealthState, long?, string?)> GetAsync(string? url, int timeoutMs = 3000)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (HealthState.Degraded, null, "URL not configured");

            try
            {
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

                var sw = Stopwatch.StartNew();
                using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();

                return (IsReachable(res) ? HealthState.Healthy : HealthState.Degraded,
                        sw.ElapsedMilliseconds,
                        $"HTTP {(int)res.StatusCode}");
            }
            catch (TaskCanceledException) { return (HealthState.Degraded, null, "Timeout"); }
            catch (Exception ex)          { return (HealthState.Down, null, ex.Message); }
        }

        /// GET /api/system/health
        [HttpGet("health")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Health(CancellationToken ct)
        {
            var cdnUrl  = _config["Cdn:BaseUrl"];               // ör: https://cdn.domain/health.txt
            var apisUrl = _config["InternalApis:HealthUrl"];    // ör: https://api.domain/health
            var rawgUrl = _config["External:RawgHealthUrl"] ?? "https://api.rawg.io/api/platforms";
            var igdbUrl = _config["External:IgdbHealthUrl"] ?? "https://api.igdb.com";

            var items = new List<HealthItemDto>();

            // 1) Server
            items.Add(Make("Server", HealthState.Healthy, 1, "OK"));

            // 2) Database
            {
                var (st, ms, det) = await PingMongoAsync(ct);
                items.Add(Make("Database", st, ms, det));
            }

            // 3) CDN (yalnızca config varsa)
            if (!string.IsNullOrWhiteSpace(cdnUrl))
            {
                var (st, ms, det) = await HeadAsync(cdnUrl, 2500);
                items.Add(Make("CDN", st, ms, det));
            }

            // 4) Internal APIs (yalnızca config varsa)
            if (!string.IsNullOrWhiteSpace(apisUrl))
            {
                var (st, ms, det) = await GetAsync(apisUrl, 3000);
                items.Add(Make("APIs", st, ms, det));
            }

            // 5) Harici örnekler (opsiyonel, istiyorsan kaldır)
            {
                var (st, ms, det) = await GetAsync(rawgUrl, 3000);
                items.Add(Make("RAWG", st, ms, det));
            }
            {
                var (st, ms, det) = await HeadAsync(igdbUrl, 2500);
                items.Add(Make("IGDB", st, ms, det));
            }

            var payload = new HealthResponseDto
            {
                Utc = DateTime.UtcNow,
                Items = items
            };

            await _logger.InfoAsync(SystemLogCategory.System, "System health checked", User?.Identity?.Name ?? "unknown");
            return Ok(payload);
        }
    }
}
