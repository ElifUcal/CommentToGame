    // Controllers/GameMergeController.cs
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Driver;
    using MongoDB.Bson;

    using CommentToGame.Data;
    using CommentToGame.Services;
    using CommentToGame.DTOs;
    using CommentToGame.Models;


    namespace CommentToGame.Controllers
    {
        // Import istek modeli
        public sealed class ImportRequest
        {
            public long? IgdbId { get; set; }
            public int? RawgId { get; set; }

            // Manual ekleme
            public string? Title { get; set; }
            public DateTime? ReleaseDate { get; set; }
            public string? Developer { get; set; }
            public string? CoverUrl { get; set; }
            public string[]? Genres { get; set; }     // sadece response için
            public string[]? Platforms { get; set; }  // sadece response için
            public string? Story { get; set; }
        }

        [ApiController]
        [Route("api/merge")]
        public sealed class GameMergeController : ControllerBase
        {
            private readonly IIgdbClient _igdb;
            private readonly IRawgClient _rawg;
            private readonly IMongoCollection<Game> _games;
            private readonly IMongoCollection<Game_Details> _gameDetails;

            public GameMergeController(IIgdbClient igdb, IRawgClient rawg, MongoDbService service)
            {
                _igdb = igdb;
                _rawg = rawg;
                _games = service.GetCollection<Game>("Games");
                _gameDetails = service.GetCollection<Game_Details>("GameDetails");
            }

            /// GET /api/merge/preview?igdbId=1020&rawgId=3498
            [HttpGet("preview")]
public async Task<IActionResult> Preview(
    [FromQuery] long igdbId,
    [FromQuery] int rawgId,
    CancellationToken ct = default)
{
    if (igdbId <= 0 || rawgId <= 0)
        return BadRequest(new { message = "igdbId ve rawgId zorunlu." });

    // 1) ANA detay çağrılarını paralel başlat
    var igdbGameTask  = _igdb.GetGameDetailAsync(igdbId, ct);
    var rawgGameTask  = _rawg.GetGameDetailAsync(rawgId);

    await Task.WhenAll(igdbGameTask, rawgGameTask);
    var igdbGame = igdbGameTask.Result;
    var rawgGame = rawgGameTask.Result;

    if (igdbGame is null)
        return NotFound(new { message = $"IGDB game not found (id={igdbId})" });
    if (rawgGame is null)
        return NotFound(new { message = $"RAWG game not found (id={rawgId})" });

    // 2) İKİNCİ DALGA: bağlı ama birbirinden bağımsız çağrıları paralel başlat
    var ttbTask         = _igdb.GetTimeToBeatAsync(igdbId, ct);
    var additionsTask   = _igdb.GetGameAdditionsAsync(igdbId, ct);
    var igdbMediaTask   = _igdb.GetMediaAsync(igdbId, ct);
    var rawgStoresTask  = _rawg.GetGameStoresAsync(rawgId);
    var rawgTeamTask    = _rawg.GetGameDevelopmentTeamAsync(rawgId);
    var rawgMediaTask   = _rawg.GetMediaAsync(rawgId, ct);

    await Task.WhenAll(ttbTask, additionsTask, igdbMediaTask, rawgStoresTask, rawgTeamTask, rawgMediaTask);

    var ttb           = ttbTask.Result;
    var additions     = additionsTask.Result;
    var (igdbScreens, igdbTrailers) = igdbMediaTask.Result;
    var rawgStores    = rawgStoresTask.Result;
    var team          = rawgTeamTask.Result;
    var (rawgScreens, rawgTrailers) = rawgMediaTask.Result;

    // DLC adları (hafifletilmiş)
    var dlcNames = additions?.Results?
        .Select(a => a.Name)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList() ?? new List<string>();

    var storeLinks = MapRawgStoresToLinks(rawgStores);
    var (cast, crew) = SplitCastCrew(team);

    // EKRAN GÖR. + TRAILER birleşimi (optimizeli)
    var mergedScreens = MergeDistinctUrls(igdbScreens, rawgScreens, max: 15);  // limit koy
    var mergedTrailers = MergeDistinctTrailers(igdbTrailers, rawgTrailers, max: 5);

    var merged = GameMerge.Merge(
        igdbGame,
        rawgGame,
        ttb,
        storeLinks,
        cast,
        crew,
        igdbDlcs: dlcNames,
        igdbScreenshots: mergedScreens,
        igdbTrailers: mergedTrailers   // ÖNEMLİ: burada birleşik seti ver
    );

    return Ok(merged);
}

// HashSet ile hızlı ve sınırlandırılmış birleştirme:
private static List<string> MergeDistinctUrls(IEnumerable<string> a, IEnumerable<string> b, int max)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var list = new List<string>(capacity: Math.Min(max, 32));

    void addRange(IEnumerable<string> src)
    {
        foreach (var u in src)
        {
            if (string.IsNullOrWhiteSpace(u)) continue;
            if (set.Add(u))
            {
                list.Add(u);
                if (list.Count >= max) return;
            }
        }
    }

    addRange(a);
    if (list.Count < max) addRange(b);
    return list;
}

private static List<TrailerDto> MergeDistinctTrailers(IEnumerable<TrailerDto> a, IEnumerable<TrailerDto> b, int max)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var list = new List<TrailerDto>(capacity: Math.Min(max, 16));

    static string KeyOf(TrailerDto t)
        => string.IsNullOrWhiteSpace(t.YouTubeId)
           ? ("url:" + (t.Url ?? ""))
           : ("yt:" + t.YouTubeId);

    void addRange(IEnumerable<TrailerDto> src)
    {
        foreach (var t in src)
        {
            if (t is null) continue;
            if (string.IsNullOrWhiteSpace(t.Url) && string.IsNullOrWhiteSpace(t.YouTubeId)) continue;

            var key = KeyOf(t);
            if (seen.Add(key))
            {
                list.Add(t);
                if (list.Count >= max) return;
            }
        }
    }

    addRange(a);
    if (list.Count < max) addRange(b);
    return list;
}


            

            // ====== Helpers ======

            private static (List<string> cast, List<string> crew) SplitCastCrew(RawgPagedCreators? team)
            {
                var cast = new List<string>();
                var crew = new List<string>();
                if (team?.Results is null) return (cast, crew);

                var castKeys = new[]
                {
                    "voice","actor","cast","narrator","motion capture","mocap","performer","stunt"
                };

                bool IsCastPosition(string pos)
                {
                    if (string.IsNullOrWhiteSpace(pos)) return false;
                    var p = pos.ToLowerInvariant();
                    return castKeys.Any(k => p.Contains(k));
                }

                foreach (var person in team.Results)
                {
                    var name = person?.Name?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var positions = person.Positions ?? new List<RawgCreatorPosition>();
                    var isCast = positions.Any(p => IsCastPosition(p?.Name ?? ""));
                    if (isCast) cast.Add(name);
                    else crew.Add(name);
                }

                cast = cast.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
                crew = crew.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
                return (cast, crew);
            }

            /// <summary>RAWG store response → StoreLink listesi</summary>
            private static List<StoreLink> MapRawgStoresToLinks(RawgPaged<RawgGameStoreItem> storesPaged)
            {
                var result = new List<StoreLink>();
                if (storesPaged?.Results is null) return result;

                foreach (var s in storesPaged.Results)
                {
                    var url = s.Url;
                    if (string.IsNullOrWhiteSpace(url)) continue;

                    string? slug = s.Store?.Slug;
                    string? storeName = s.Store?.Name;
                    string? domain = s.Store?.Domain;
                    string? externalId = null;

                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host.ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(storeName))
                        {
                            var guess = GuessStoreFromHost(host);
                            slug = slug ?? guess.slug;
                            storeName = storeName ?? guess.name;
                        }
                        if (string.IsNullOrWhiteSpace(domain))
                            domain = host;

                        externalId = ExtractExternalIdFromUrl(host, slug, url, uri.AbsolutePath);
                    }

                    result.Add(new StoreLink
                    {
                        StoreId = s.Store?.Id ?? s.StoreId,
                        Store = storeName ?? "Store",
                        Slug = slug,
                        Domain = domain,
                        Url = url,
                        ExternalId = externalId,
                        Price = null,
                    });
                }

                // yinelenenleri eleyelim
                result = result
                    .GroupBy(x => (x.Store, x.ExternalId ?? x.Url))
                    .Select(g => g.First())
                    .ToList();

                return result;
            }

            private static (string slug, string name) GuessStoreFromHost(string host)
            {
                if (host.Contains("steampowered.com")) return ("steam", "Steam");
                if (host.Contains("gog.com")) return ("gog", "GOG");
                if (host.Contains("epicgames.com")) return ("epic-games", "Epic Games Store");
                if (host.Contains("playstation.com")) return ("playstation-store", "PlayStation Store");
                if (host.Contains("xbox.com") || host.Contains("microsoft.com") || host.Contains("marketplace.xbox.com"))
                    return ("xbox-store", "Xbox Store");
                if (host.Contains("nintendo.com")) return ("nintendo-eshop", "Nintendo eShop");
                return ("store", "Store");
            }

            private static string? ExtractExternalIdFromUrl(string host, string? slug, string url, string path)
            {
                var key = slug;
                if (string.IsNullOrWhiteSpace(key))
                {
                    if (host.Contains("steampowered.com")) key = "steam";
                    else if (host.Contains("gog.com")) key = "gog";
                    else if (host.Contains("epicgames.com")) key = "epic-games";
                    else if (host.Contains("playstation.com")) key = "playstation-store";
                    else if (host.Contains("xbox.com") || host.Contains("microsoft.com") || host.Contains("marketplace.xbox.com")) key = "xbox-store";
                    else if (host.Contains("nintendo.com")) key = "nintendo-eshop";
                    else key = "";
                }

                switch (key)
                {
                    case "steam":
                    {
                        var m = Regex.Match(path, @"/(?:app|sub)/(\d+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }
                    case "gog":
                    {
                        var m = Regex.Match(path, @"/game/([^/?#]+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }
                    case "epic-games":
                    {
                        var m = Regex.Match(path, @"/p/([^/?#]+)", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;
                        m = Regex.Match(path, @"/product/([^/?#]+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }
                    case "playstation-store":
                    {
                        var m = Regex.Match(path, @"/concept/(\d+)", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;
                        m = Regex.Match(path, @"/product/([A-Z0-9_-]+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }
                    case "xbox-store":
                    {
                        var m = Regex.Match(path, @"/store/.+?/([A-Z0-9]{6,})", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;

                        m = Regex.Match(url, @"([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;

                        var last = path.TrimEnd('/').Split('/').LastOrDefault();
                        return string.IsNullOrWhiteSpace(last) ? null : last;
                    }
                    case "nintendo-eshop":
                    {
                        var last = path.TrimEnd('/').Split('/').LastOrDefault();
                        return string.IsNullOrWhiteSpace(last) ? null : last;
                    }
                    default:
                        return null;
                }
            }
        }
    }
