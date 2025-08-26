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

// IGDB nested tiplerini kısayla kullanmak için:
using static CommentToGame.DTOs.IGdbDto;

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

            IgdbGameDetail? igdbGame = await _igdb.GetGameDetailAsync(igdbId, ct);
            if (igdbGame is null)
                return NotFound(new { message = $"IGDB game not found (id={igdbId})" });

            IgdbTimeToBeat? ttb = await _igdb.GetTimeToBeatAsync(igdbId, ct);

            RawgGameDetail? rawgGame = await _rawg.GetGameDetailAsync(rawgId);
            if (rawgGame is null)
                return NotFound(new { message = $"RAWG game not found (id={rawgId})" });

            // Store linkleri
            var rawgStores = await _rawg.GetGameStoresAsync(rawgId);
            var storeLinks = MapRawgStoresToLinks(rawgStores);

            // IGDB additions → DLC adları
            var additions = await _igdb.GetGameAdditionsAsync(igdbId, ct);
            var dlcNames = additions?.Results?
                .Select(a => a.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            // RAWG dev team → cast/crew
            var team = await _rawg.GetGameDevelopmentTeamAsync(rawgId);
            var (cast, crew) = SplitCastCrew(team);

            var merged = GameMerge.Merge(igdbGame, rawgGame, ttb, storeLinks, cast, crew);
            if (dlcNames.Count > 0) merged.Dlcs = dlcNames;

            return Ok(merged);
        }

        /// POST /api/merge/import
        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] ImportRequest req, CancellationToken ct = default)
        {
            if (req is null) return BadRequest(new { message = "Body gerekli." });

            // ------------ 1) MANUAL ------------
            if (req.IgdbId is null && req.RawgId is null)
            {
                if (string.IsNullOrWhiteSpace(req.Title))
                    return BadRequest(new { message = "Manual eklemede Title zorunlu." });

                var game = new Game
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Game_Name = req.Title!.Trim(),
                    Release_Date = req.ReleaseDate,
                    Studio = req.Developer,
                    Main_image_URL = req.CoverUrl,
                    Createdat = DateTime.UtcNow
                };
                await _games.InsertOneAsync(game, cancellationToken: ct);

                var det = new Game_Details
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    GameId = game.Id,
                    Developer = req.Developer,
                    Story = req.Story,
                    GenreIds = null,
                    PlatformIds = null
                };
                await _gameDetails.InsertOneAsync(det, cancellationToken: ct);

                return Ok(new
                {
                    id = game.Id,
                    cover = game.Main_image_URL,
                    title = game.Game_Name,
                    release = game.Release_Date,
                    developer = det.Developer ?? game.Studio,
                    genres = (IEnumerable<string>) (req.Genres ?? Array.Empty<string>()),
                    platforms = (IEnumerable<string>) (req.Platforms ?? Array.Empty<string>()),
                    story = det.Story
                });
            }

            // ------------ 2) RAWG/IGDB (merge) ------------
            IgdbGameDetail? igdbGame = null;
            IgdbTimeToBeat? ttb = null;
            RawgGameDetail? rawgGame = null;

            if (req.IgdbId is long igdbId && igdbId > 0)
            {
                igdbGame = await _igdb.GetGameDetailAsync(igdbId, ct);
                if (igdbGame is not null)
                    ttb = await _igdb.GetTimeToBeatAsync(igdbId, ct);
            }

            if (req.RawgId is int rawgId && rawgId > 0)
            {
                rawgGame = await _rawg.GetGameDetailAsync(rawgId);
            }

            if (igdbGame is null && rawgGame is null)
                return BadRequest(new { message = "Geçerli igdbId/rawgId bulunamadı." });

            // store links + team
            var storeLinks = new List<StoreLink>();
            if (rawgGame is not null)
            {
                var stores = await _rawg.GetGameStoresAsync(rawgGame.Id);
                storeLinks = MapRawgStoresToLinks(stores);
            }

            var team = rawgGame is not null ? await _rawg.GetGameDevelopmentTeamAsync(rawgGame.Id) : null;
            var (cast, crew) = SplitCastCrew(team);

            var merged = GameMerge.Merge(igdbGame, rawgGame, ttb, storeLinks, cast, crew);

            // ---- DB'ye yaz ----
            var gameDoc = new Game
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Game_Name = merged.Name ?? rawgGame?.Name ?? igdbGame?.Name ?? "Untitled",
                Release_Date = merged.ReleaseDate,
                Main_image_URL = merged.MainImage,
                Studio = merged.Developer ?? merged.Publisher,
                Createdat = DateTime.UtcNow,
                GgDb_Rating = merged.GgDbRating,
                Metacritic_Rating = merged.Metacritic,
                Cast = cast,
                Crew = crew
            };
            await _games.InsertOneAsync(gameDoc, cancellationToken: ct);

            var detDoc = new Game_Details
            {
                Id = ObjectId.GenerateNewId().ToString(),
                GameId = gameDoc.Id,
                Developer = merged.Developer ?? gameDoc.Studio,
                Publisher = merged.Publisher,
                Story = merged.About,
                // Id tabanlı eşleşme henüz yok → GenreIds/PlatformIds boş bırakılıyor.
                GenreIds = null,
                PlatformIds = null,
                // Store linkleri ve DLC'ler bu modelde:
                Store_Links = storeLinks ?? new List<StoreLink>(),
                DLCs = merged.Dlcs?.ToList() ?? new List<string>()
            };
            await _gameDetails.InsertOneAsync(detDoc, cancellationToken: ct);

            // Frontend liste kartı şekli
            return Ok(new
            {
                id = gameDoc.Id,
                cover = gameDoc.Main_image_URL,
                title = gameDoc.Game_Name,
                release = gameDoc.Release_Date,
                developer = detDoc.Developer ?? gameDoc.Studio,
                genres = merged.Genres ?? Enumerable.Empty<string>(),
                platforms = merged.Platforms ?? Enumerable.Empty<string>(),
                story = detDoc.Story
            });
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
                    ExternalId = externalId
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
