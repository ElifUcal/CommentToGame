// Controllers/GameMergeController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using CommentToGame.Services;
using CommentToGame.DTOs;
using CommentToGame.Models;
using static CommentToGame.DTOs.IGdbDto;

namespace CommentToGame.Controllers
{
    [ApiController]
    [Route("api/merge")]
    public sealed class GameMergeController : ControllerBase
    {
        private readonly IIgdbClient _igdb;
        private readonly IRawgClient _rawg;

        public GameMergeController(IIgdbClient igdb, IRawgClient rawg)
        {
            _igdb = igdb;
            _rawg = rawg;
        }

        /// <summary>
        /// IGDB + RAWG verilerini birleştirip (import etmeden) tek JSON döner.
        /// Örnek: GET /api/merge/preview?igdbId=1020&rawgId=3498
        /// </summary>
        [HttpGet("preview")]
        public async Task<IActionResult> Preview(
    [FromQuery] long igdbId,
    [FromQuery] int rawgId,
    CancellationToken ct = default)
        {
            if (igdbId <= 0 || rawgId <= 0)
                return BadRequest(new { message = "igdbId ve rawgId zorunlu." });

            var igdb = await _igdb.GetGameDetailAsync(igdbId, ct);
            if (igdb is null)
                return NotFound(new { message = $"IGDB game not found (id={igdbId})" });

            var ttb = await _igdb.GetTimeToBeatAsync(igdbId, ct);
            var rawg = await _rawg.GetGameDetailAsync(rawgId); // RAWG ct’siz
            if (rawg is null)
                return NotFound(new { message = $"RAWG game not found (id={rawgId})" });

            // Store linkleri
            var rawgStores = await _rawg.GetGameStoresAsync(rawgId);
            var storeLinks = MapRawgStoresToLinks(rawgStores);

            // DLC’ler (IGDB additions)
            var additions = await _igdb.GetGameAdditionsAsync(igdbId, ct);
            var dlcNames = additions?.Results?
                .Select(a => a.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            // CAST/CREW — yeni
            var team = await _rawg.GetGameDevelopmentTeamAsync(rawgId);
            var (cast, crew) = SplitCastCrew(team);

            // Merge: cast/crew ve storeLinks'i geçir
            var merged = GameMerge.Merge(igdb, rawg, ttb, storeLinks, cast, crew);

            // DLCleri merge sonucu üzerine yaz
            if (dlcNames.Count > 0)
                merged.Dlcs = dlcNames;

            return Ok(merged);
        }

        private static (List<string> cast, List<string> crew) SplitCastCrew(RawgPagedCreators? team)
        {
            var cast = new List<string>();
            var crew = new List<string>();
            if (team?.Results == null) return (cast, crew);

            // Cast sayılacak pozisyon anahtarları
            var castKeys = new[]
            {
        "voice", "actor", "cast", "narrator", "motion capture", "mocap",
        "performer", "stunt"
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

            // Dedupe (case-insensitive) ve sıralama
            cast = cast.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            crew = crew.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

            return (cast, crew);
        }



        /// <summary>
        /// Sadece RAWG store link mapping çıktısını görmek için yardımcı endpoint
        /// Örnek: GET /api/merge/stores?rawgId=3498
        /// </summary>
        [HttpGet("stores")]
        public async Task<IActionResult> Stores([FromQuery] int rawgId)
        {
            if (rawgId <= 0) return BadRequest(new { message = "rawgId zorunlu." });
            var rawgStores = await _rawg.GetGameStoresAsync(rawgId);
            var storeLinks = MapRawgStoresToLinks(rawgStores);
            return Ok(storeLinks);
        }

        // ===================== Store mapper =====================

        private static List<StoreLink> MapRawgStoresToLinks(RawgPaged<RawgGameStoreItem> storesPaged)
        {
            var result = new List<StoreLink>();
            if (storesPaged?.Results == null) return result;

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

                    // Store bilgisi yoksa hosttan tahmin et
                    if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(storeName))
                    {
                        var guess = GuessStoreFromHost(host);
                        slug = slug ?? guess.slug;
                        storeName = storeName ?? guess.name;
                    }

                    // Domain boşsa URL hostunu kullan
                    if (string.IsNullOrWhiteSpace(domain))
                        domain = host;

                    // Slug/hosta göre externalId çıkar
                    externalId = ExtractExternalIdFromUrl(host, slug, url, uri.AbsolutePath);
                }

                result.Add(new StoreLink
                {
                    StoreId    = s.Store?.Id ?? s.StoreId,
                    Store      = storeName ?? "Store",
                    Slug       = slug,
                    Domain     = domain,
                    Url        = url,
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
            if (host.Contains("steampowered.com"))                 return ("steam", "Steam");
            if (host.Contains("gog.com"))                          return ("gog", "GOG");
            if (host.Contains("epicgames.com"))                    return ("epic-games", "Epic Games Store");
            if (host.Contains("playstation.com"))                  return ("playstation-store", "PlayStation Store");
            if (host.Contains("xbox.com") || host.Contains("microsoft.com") || host.Contains("marketplace.xbox.com"))
                                                                   return ("xbox-store", "Xbox Store");
            if (host.Contains("nintendo.com"))                     return ("nintendo-eshop", "Nintendo eShop");
            return ("store", "Store");
        }

        private static string? ExtractExternalIdFromUrl(string host, string? slug, string url, string path)
        {
            // Slug boşsa hosttan türet
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
                    // https://store.steampowered.com/app/3240220/...
                    {
                        var m = Regex.Match(path, @"/(?:app|sub)/(\d+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }

                case "gog":
                    // https://www.gog.com/game/the_witcher_3_wild_hunt
                    {
                        var m = Regex.Match(path, @"/game/([^/?#]+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }

                case "epic-games":
                    // /p/<slug>  veya  /product/<slug>/home
                    {
                        var m = Regex.Match(path, @"/p/([^/?#]+)", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;
                        m = Regex.Match(path, @"/product/([^/?#]+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }

                case "playstation-store":
                    // /concept/204794  veya  /product/UP1004-CUSA00419_00-GTAVDIGITALDOWNL
                    {
                        var m = Regex.Match(path, @"/concept/(\d+)", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;
                        m = Regex.Match(path, @"/product/([A-Z0-9_-]+)", RegexOptions.IgnoreCase);
                        return m.Success ? m.Groups[1].Value : null;
                    }

                case "xbox-store":
                    // https://www.xbox.com/.../STORE_ID  (BPJ686W6S0NH)
                    // https://www.microsoft.com/.../store/p/.../BPJ686W6S0NH
                    // http://marketplace.xbox.com/.../66acd000-...-d802545408a7
                    {
                        var m = Regex.Match(path, @"/store/.+?/([A-Z0-9]{6,})", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;

                        m = Regex.Match(url, @"([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})", RegexOptions.IgnoreCase);
                        if (m.Success) return m.Groups[1].Value;

                        var last = path.TrimEnd('/').Split('/').LastOrDefault();
                        return string.IsNullOrWhiteSpace(last) ? null : last;
                    }

                case "nintendo-eshop":
                    // Genelde son segment
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
