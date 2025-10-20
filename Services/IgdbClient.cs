using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.DTOs;
using CommentToGame.Models;
using Microsoft.Extensions.Configuration;
using static CommentToGame.DTOs.IGdbDto;

namespace CommentToGame.Services;

/// <summary>
/// IIgdbClient implementasyonu – IGDB v4 POST istekleri ile çalışır.
/// </summary>
public sealed class IgdbClient : IIgdbClient
{
    private readonly IHttpClientFactory _http;
    private readonly IgdbAuthService _auth;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IgdbClient(IHttpClientFactory http, IgdbAuthService auth)
    { _http = http; _auth = auth; }

    // ------------------------- Low-level POST helper -------------------------
    private async Task<T[]> PostAsync<T>(string endpoint, string igdbQuery, CancellationToken ct)
    {
        var http = _http.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint}")
        {
            Content = new StringContent(igdbQuery, Encoding.UTF8, "text/plain")
        };
        await _auth.AddAuthAsync(req, ct);
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<T[]>(stream, _json, ct);
        return data ?? Array.Empty<T>();
    }

    // ------------------------- Public API (IIgdbClient) -------------------------
    public async Task<IgdbPagedGames> GetGamesAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (page <= 0) page = 1; if (pageSize <= 0) pageSize = 40;
        var offset = (page - 1) * pageSize;

        var games = await PostAsync<GameRow>(
            "games",
            $"fields id,name,first_release_date,genres,platforms,cover,age_ratings,keywords,involved_companies,game_engines; " +
            $"where version_parent = null; sort first_release_date desc; limit {pageSize}; offset {offset};",
            ct);

        return new IgdbPagedGames
        {
            Results = games.Select(g => new IgdbGameCard { Id = g.id, Name = g.name ?? $"game-{g.id}" }).ToList(),
            Next = games.Length < pageSize ? null : "next"
        };
    }

    public async Task<IgdbPagedGames> SearchGamesAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) query = "";
        if (page <= 0) page = 1; if (pageSize <= 0) pageSize = 40;
        var offset = (page - 1) * pageSize;
        var safe = query.Replace("\"", "\\\"");

        // gamesten alt alanları doğrudan getiriyoruz (cover.image_id, platforms.name, category)
          var rows = await PostAsync<GameRowSearch>(
        "games",
        $@"fields id,name,first_release_date,category,cover.image_id,platforms.name;
           where name ~ ""{safe}""*;
           limit {pageSize}; offset {offset};",
        ct);

        var items = rows.Select(r =>
        {
            string? coverUrl = !string.IsNullOrWhiteSpace(r.cover?.image_id)
                ? $"https://images.igdb.com/igdb/image/upload/t_cover_big/{r.cover.image_id}.jpg"
                : null;

            int? year = r.first_release_date.HasValue
                ? (int?)DateTimeOffset.FromUnixTimeSeconds(r.first_release_date.Value).UtcDateTime.Year
                : null;

            return new IgdbGameCard
            {
                Id = r.id,
                Name = r.name ?? $"game-{r.id}",
                Year = year,
                Cover = coverUrl,
                Platforms = r.platforms?.Select(p => p.name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList() ?? new List<string>(),
                Category = CatToText(r.category)
            };
        }).ToList();




        return new IgdbPagedGames
        {
            Results = items,
            Next = rows.Length < pageSize ? null : "next"
        };
    }

    public async Task<IgdbPagedGames> SearchGamesSmartAsync(string query, int page, int pageSize, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(query)) query = "";
    if (page <= 0) page = 1;
    if (pageSize <= 0) pageSize = 40;

    var offset = (page - 1) * pageSize;
    var safe   = query.Replace("\"", "\\\"");
    var toks   = Tokens(query);

    // --- normalize & slug (™, ®, noktalama vs. toleransı için)
    var normQ  = NormalizeName(query);
    var slugQ  = SlugifyStrict(query);

    // === PRE-FLIGHT A: slug ile ana oyun
    var exactBySlug = await PostAsync<GameRowSearch>(
        "games",
        $@"fields id,name,first_release_date,category,cover.image_id,platforms.name,parent_game,version_parent,slug;
           where slug = ""{slugQ}"" & category = 0 & parent_game = null & version_parent = null;
           limit 1;",
        ct);

    // === PRE-FLIGHT B: name ~ "query"* ile ana oyun (™ farkı için ‘=’ yerine ‘~’)
    var exactByNameLoose = await PostAsync<GameRowSearch>(
        "games",
        $@"fields id,name,first_release_date,category,cover.image_id,platforms.name,parent_game,version_parent,slug;
           where name ~ ""{safe}""* & category = 0 & parent_game = null & version_parent = null;
           limit 3;",
        ct);

    // === PRE-FLIGHT C: alternative_names → game id’lerini çek
    var altRows = await PostAsync<AltNameRow>(
        "alternative_names",
        $@"fields id,name,game;
           where name ~ ""{safe}""*;
           limit 50;",
        ct);

    var altIds   = altRows.Where(a => a.game.HasValue).Select(a => a.game!.Value).Distinct().ToArray();
    GameRowSearch[] fromAlts = Array.Empty<GameRowSearch>();
    if (altIds.Length > 0)
    {
        fromAlts = await PostAsync<GameRowSearch>(
            "games",
            $@"fields id,name,first_release_date,category,cover.image_id,platforms.name,parent_game,version_parent,slug;
               where id = ({string.Join(',', altIds)});
               limit {Math.Min(altIds.Length, 200)};",
            ct);
    }

    // === 1) /v4/search: ID’leri topla (kapsamı genişlet)
    var searchRows = await PostAsync<SearchRow>(
        "search",
        $@"fields name, game;
           search ""{safe}"";
           limit {Math.Min(pageSize * 3, 100)};
           offset {offset};",
        ct);

    var ids = searchRows.Where(s => s.game.HasValue).Select(s => s.game!.Value).Distinct().ToList();
    if (ids.Count == 0)
        return await SearchGamesAsync(query, page, pageSize, ct);

    // === 2) /v4/games: ana oyun filtresi
    var rowsMainOnly = await PostAsync<GameRowSearch>(
        "games",
        $@"fields id,name,first_release_date,category,cover.image_id,platforms.name,parent_game,version_parent,slug;
           where id = ({string.Join(",", ids)})
             & category = 0
             & parent_game = null
             & version_parent = null;
           limit {ids.Count};",
        ct);

    // === 3) /v4/games: tüm sonuçlar
    var rowsAll = await PostAsync<GameRowSearch>(
        "games",
        $@"fields id,name,first_release_date,category,cover.image_id,platforms.name,parent_game,version_parent,slug;
           where id = ({string.Join(",", ids)});
           limit {ids.Count};",
        ct);

    // === 4) Birleştir + tekilleştir
    var combined = exactBySlug
        .Concat(exactByNameLoose)
        .Concat(fromAlts)
        .Concat(rowsMainOnly)
        .Concat(rowsAll)
        .GroupBy(r => r.id)
        .Select(g => g.First())
        .ToList();

    // === 5) Token filtresi
    var filtered = combined.Where(r => ContainsAllTokens(r.name, toks)).ToList();
    if (filtered.Count == 0 && toks.Count > 0)
        filtered = combined.Where(r => (r.name ?? "").ToLowerInvariant().Contains(toks[0])).ToList();
    if (filtered.Count == 0)
        filtered = combined;

    // === (Opsiyonel) normalize/slug eşleşen ana oyunu en üste pin’le
    var mainCandidates = filtered.Where(r =>
        (r.category ?? 99) == 0 && !r.parent_game.HasValue && !r.version_parent.HasValue).ToList();

    var topMain = mainCandidates.FirstOrDefault(r =>
        NormalizeName(r.name ?? "") == normQ ||
        (r.slug ?? "").Equals(slugQ, StringComparison.OrdinalIgnoreCase));

    if (topMain is not null)
        filtered = new[] { topMain }.Concat(filtered.Where(r => r.id != topMain.id)).ToList();

    // === 6) Sıralama: normalize tam eşleşme > slug tam eşleşme > ana oyun > (kalanlar)
    var ordered = filtered
        .OrderByDescending(r => NormalizeName(r.name ?? "") == normQ)
        .ThenByDescending(r => (r.slug ?? "").Equals(slugQ, StringComparison.OrdinalIgnoreCase))
        .ThenBy(r => r.category ?? int.MaxValue)                // 0 (Main Game) öne
        .ThenBy(r => r.parent_game.HasValue ? 1 : 0)            // parent_game = null öne
        .ThenBy(r => r.version_parent.HasValue ? 1 : 0)         // version_parent = null öne
        .ThenBy(r => r.first_release_date ?? long.MaxValue)
        .ToList();

    // === 7) Map & return
    var items = ordered.Select(MapToCard).ToList();
    var next  = searchRows.Length < Math.Min(pageSize * 3, 100) ? null : "next";
    return new IgdbPagedGames { Results = items, Next = next };

    // ---- helpers ----
    static string NormalizeName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var lowered = s.ToLowerInvariant();
        lowered = lowered.Replace("™", "").Replace("®", "");
        lowered = Regex.Replace(lowered, @"[’'`]", "");
        lowered = Regex.Replace(lowered, @"[^\p{L}\p{Nd}\s]+", " ");
        lowered = Regex.Replace(lowered, @"\s+", " ").Trim();
        return lowered;
    }

    static string SlugifyStrict(string s) =>
        Regex.Replace(s.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    static string Slugify(string s) => // mevcut kodunla uyumlu kalsın
        Regex.Replace(s.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    static List<string> Tokens(string s)
    {
        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "the", "a", "an", "of", "and" };
        return Regex.Split(s, @"\W+")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.ToLowerInvariant())
                    .Where(t => !stop.Contains(t))
                    .ToList();
    }

    static bool ContainsAllTokens(string? name, List<string> toks)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant();
        return toks.All(t => lower.Contains(t));
    }

    IgdbGameCard MapToCard(GameRowSearch r)
    {
        string? coverUrl = !string.IsNullOrWhiteSpace(r.cover?.image_id)
            ? $"https://images.igdb.com/igdb/image/upload/t_cover_big/{r.cover.image_id}.jpg"
            : null;

        int? year = r.first_release_date.HasValue
            ? (int?)DateTimeOffset.FromUnixTimeSeconds(r.first_release_date.Value).UtcDateTime.Year
            : null;

        return new IgdbGameCard
        {
            Id        = r.id,
            Name      = r.name ?? $"game-{r.id}",
            Year      = year,
            Cover     = coverUrl,
            Platforms = r.platforms?.Select(p => p.name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList()
                        ?? new List<string>(),
            Category  = CatToText(r.category)
        };
    }
}




//Smart yardımcı dto
private sealed class AltNameRow
{
    public long id { get; set; }
    public string? name { get; set; }
    public long? game { get; set; }
}




    // DTO: IGDB /v4/search cevabı için
    public sealed class SearchRow
    {
        public string? name { get; set; }
        public long? game { get; set; } // games tablosundaki id
    }

// IGDB category enumunu metne çevir
private static string? CatToText(long? cat) => cat switch
{
    0  => "Main Game",
    1  => "DLC/Add-on",
    2  => "Expansion",
    3  => "Bundle",
    4  => "Standalone Expansion",
    5  => "Mod",
    6  => "Episode",
    7  => "Season",
    8  => "Remake",
    9  => "Remaster",
    10 => "Expanded Game",
    11 => "Port",
    12 => "Fork",
    13 => "Pack",
    _  => null
};

// Bu arama modeli, cover/platform isimlerini embed eder
private sealed class GameRowSearch
{
    public long id { get; set; }
    public string? name { get; set; }
    public long? first_release_date { get; set; }
    public long? category { get; set; }
    public CoverObj? cover { get; set; }
    public PlatformObj[]? platforms { get; set; }

    // yeni alanlar:
    public long? parent_game { get; set; }
    public long? version_parent { get; set; }
    public string? slug { get; set; }

    public sealed class CoverObj { public string? image_id { get; set; } }
    public sealed class PlatformObj { public string? name { get; set; } }
}




    public async Task<List<StoreLink>> GetStoreLinksAsync(long gameId, CancellationToken ct = default)
    {
        var links = new List<StoreLink>();

        // 1) IGDB websites
        var sites = await PostAsync<WebsiteRow>(
            "websites",
            $"fields id,category,url; where game = {gameId}; limit 500;",
            ct);

        foreach (var s in sites)
        {
            if (string.IsNullOrWhiteSpace(s.url)) continue;
            var sl = MakeStoreLinkFromUrl(s.url!, includeAmazon: false); // Amazon'ı gürültü olmasın diye kapalı
            if (sl != null) links.Add(sl);
        }

        // 2) external_games – bazen Steam/Epic UID olur
        var exts = await PostAsync<ExternalGameRow>(
            "external_games",
            $"fields category,url,uid; where game = {gameId}; limit 500;",
            ct);

        foreach (var e in exts)
        {
            if (!string.IsNullOrWhiteSpace(e.url))
            {
                var fromUrl = MakeStoreLinkFromUrl(e.url!, includeAmazon: false);
                if (fromUrl != null)
                {
                    if (!string.IsNullOrWhiteSpace(e.uid))
                    {
                        var uid = Regex.Replace(e.uid!, @"^xbox360", "", RegexOptions.IgnoreCase);

                        if (string.Equals(fromUrl.Store, "Xbox Store", StringComparison.OrdinalIgnoreCase))
                            fromUrl.ExternalId = uid;                    // Xboxta normalize UID
                        else if (string.IsNullOrWhiteSpace(fromUrl.ExternalId))
                            fromUrl.ExternalId = uid;                     // diğerlerinde boşsa doldur
                    }

                    links.Add(fromUrl);
                }
            }
            else if (!string.IsNullOrWhiteSpace(e.uid))
            {
                var uid = Regex.Replace(e.uid!, @"^xbox360", "", RegexOptions.IgnoreCase);

                // Steam AppID gibi saf numerik ise bir Steam linki üret
                if (Regex.IsMatch(uid, @"^\d+$"))
                {
                    links.Add(new StoreLink
                    {
                        Store = "Steam",
                        Slug = "steam",
                        Domain = "store.steampowered.com",
                        Url = $"https://store.steampowered.com/app/{uid}/",
                        ExternalId = uid
                    });
                }
            }
        }



        // 3) normalize + dedupe (Store + ExternalId öncelikli; yoksa Domain+Path)
        string Key(StoreLink l)
        {
            if (!string.IsNullOrWhiteSpace(l.Store) && !string.IsNullOrWhiteSpace(l.ExternalId))
                return $"{l.Store}|{l.ExternalId}".ToLowerInvariant();

            if (Uri.TryCreate(l.Url ?? "", UriKind.Absolute, out var u))
                return $"{l.Domain}|{u.AbsolutePath}".ToLowerInvariant();

            return (l.Url ?? "").ToLowerInvariant();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        links = links
            .Where(l => seen.Add(Key(l)))
            .ToList();

        // 4) Küçük kozmetik: aynı mağazadan birden çok link varsa ExternalId olanı tercih et
        links = links
            .GroupBy(l => l.Store, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var withId = g.Where(x => !string.IsNullOrWhiteSpace(x.ExternalId)).ToList();
                return withId.Count > 0 ? withId : g.ToList();
            })
            .SelectMany(x => x)
            .ToList();

        return links;
    }

    public async Task<IgdbGameDetail?> GetGameDetailAsync(long id, CancellationToken ct = default)
    {
        var rows = await PostAsync<GameRow>(
            "games",
            $"fields id,name,summary,first_release_date,genres,platforms,cover,age_ratings,keywords,involved_companies,game_engines; where id = {id}; limit 1;",
            ct);
        var g = rows.FirstOrDefault();
        if (g is null) return null;

        // Resolve lookups
        var genreNames = g.genres is { Length: > 0 } ? await ResolveNames("genres", g.genres!, ct) : new List<string>();
        var platformNames = g.platforms is { Length: > 0 } ? await ResolveNames("platforms", g.platforms!, ct) : new List<string>();

        var (developers, publishers) = await ResolveCompaniesAsync(g.involved_companies, ct);
        var ageNames = await ResolveAgeRatingsAsync(g.age_ratings, ct);
        var tagNames = g.keywords is { Length: > 0 } ? await ResolveKeywordsAsync(g.keywords!, ct) : new List<string>();
        var awards = ExtractAwardsFromTags(tagNames);

        string? coverUrl = null;
        if (g.cover.HasValue)
        {
            var cover = await PostAsync<CoverRow>("covers", $"fields image_id; where id = {g.cover.Value}; limit 1;", ct);
            var img = cover.FirstOrDefault()?.image_id;
            if (!string.IsNullOrWhiteSpace(img))
                coverUrl = $"https://images.igdb.com/igdb/image/upload/t_cover_big/{img}.jpg";
        }

        var (audioLangs, subtitleLangs, uiLangs) = await ResolveLanguagesAsync(g.id, ct);
        var contentWarnings = await ResolveContentWarningsAsync(g.age_ratings, ct);

        var engineNames = g.game_engines is { Length: > 0 }
    ? await ResolveNames("game_engines", g.game_engines!, ct)
    : new List<string>();

        return new IgdbGameDetail
        {
            Id = g.id,
            Name = g.name ?? string.Empty,
            Summary = g.summary,
            ReleaseDate = FromUnix(g.first_release_date),
            Metacritic = null, // IGDB rating/aggregated_rating ayrı; isterseniz ekleyebiliriz
            Rating = null,     // İsteğe bağlı: games alanlarına rating eklenebilir
            BackgroundImage = coverUrl,
            Added = null,
            Genres = genreNames,
            Platforms = platformNames,
            Developers = developers,
            Publishers = publishers,
            AgeRatings = ageNames,
            Tags = tagNames,
            Awards = awards,
            Engines = engineNames,
            AudioLanguages = audioLangs,
            Subtitles = subtitleLangs,
            InterfaceLanguages = uiLangs,
            ContentWarnings = contentWarnings

        };
    }

    private async Task<(List<string> audio, List<string> subs, List<string> ui)>
    ResolveLanguagesAsync(long gameId, CancellationToken ct)
    {
        // 1) language_supports: language + language_support_type
        var ls = await PostAsync<LangSupportRow>(
            "language_supports",
            $"fields language,language_support_type; where game = {gameId}; limit 500;",
            ct);

        if (ls.Length == 0) return (new(), new(), new());

        var langIds = ls.Select(x => x.language).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var typeIds = ls.Select(x => x.language_support_type).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();

        // 2) languages → id→name
        var langs = await PostAsync<IdNameRow>(
            "languages",
            $"fields id,name; where id = ({string.Join(',', langIds)}); limit 500;",
            ct);
        var langMap = langs.ToDictionary(x => x.id, x => x.name ?? $"lang-{x.id}");

        // 3) language_support_types → id→name  (ör. "audio", "subtitles", "interface")
        var types = await PostAsync<IdNameRow>(
            "language_support_types",
            $"fields id,name; where id = ({string.Join(',', typeIds)}); limit 500;",
            ct);
        var typeMap = types.ToDictionary(x => x.id, x => (x.name ?? "").ToLowerInvariant());

        var audio = new List<string>();
        var subs = new List<string>();
        var ui = new List<string>();

        foreach (var row in ls)
        {
            if (!row.language.HasValue || !row.language_support_type.HasValue) continue;
            if (!langMap.TryGetValue(row.language.Value, out var lname)) continue;

            var t = typeMap.TryGetValue(row.language_support_type.Value, out var tn) ? tn : "";
            // Esnek eşleştirme (dokümanda tip adları "audio", "subtitles", "interface")
            if (t.Contains("audio") || t.Contains("voice")) audio.Add(lname);
            else if (t.Contains("subtitle") || t.Contains("text")) subs.Add(lname);
            else if (t.Contains("interface") || t.Contains("ui")) ui.Add(lname);
        }

        return (audio.Distinct().ToList(), subs.Distinct().ToList(), ui.Distinct().ToList());
    }

    private static string IgdbImage(string imageId, string size = "t_screenshot_big")
    => $"https://images.igdb.com/igdb/image/upload/{size}/{imageId}.jpg";

private static TrailerDto IgdbVideoToTrailer(string videoId) => new TrailerDto
{
    Platform  = "youtube",
    Url       = $"https://www.youtube.com/watch?v={videoId}",
    YouTubeId = videoId
};

// IGDB: games -> screenshots.image_id & videos.video_id
// Services/IgdbClient.cs  (sınıfın içine ekle)

public async Task<(List<string> screenshots, List<TrailerDto> trailers)> GetMediaAsync(long gameId, CancellationToken ct = default)
{
    // 1) Screenshots
    var ssRows = await PostAsync<ScreenshotRow>(
        "screenshots",
        $"fields image_id; where game = {gameId}; limit 100;",
        ct);

    // IGDB image url formatı: https://images.igdb.com/igdb/image/upload/<size>/<image_id>.jpg
    // Uygun size: t_screenshot_big (ya da t_1080p)
    var screenshots = ssRows
        .Select(r => r.image_id)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => $"https://images.igdb.com/igdb/image/upload/t_screenshot_big/{id}.jpg")
        .Distinct()
        .ToList();

    // 2) Game videos (YouTube)
    var vidRows = await PostAsync<GameVideoRow>(
        "game_videos",
        $"fields video_id,name; where game = {gameId}; limit 100;",
        ct);

    var trailers = vidRows
        .Select(v => new TrailerDto
        {
            Platform  = "YouTube",
            YouTubeId = v.video_id,
            Url       = string.IsNullOrWhiteSpace(v.video_id) ? null : $"https://www.youtube.com/watch?v={v.video_id}",
            
        })
        .Where(t => !string.IsNullOrWhiteSpace(t.YouTubeId) || !string.IsNullOrWhiteSpace(t.Url))
        .DistinctBy(t => t.YouTubeId ?? t.Url) // .NET 6+ varsa; yoksa GroupBy ile yap
        .ToList();

    return (screenshots, trailers);
}

// --- private row modellerini dosyanın altına ekle ---
private sealed class ScreenshotRow { public long id { get; set; } public string? image_id { get; set; } }
private sealed class GameVideoRow  { public long id { get; set; } public string? video_id { get; set; } public string? name { get; set; } }




    public async Task<List<string>> GetAwardsLikeEventsAsync(long gameId, CancellationToken ct = default)
    {
        // Oyunun yer aldığı etkinlikleri çek
        var rows = await PostAsync<EventRow>(
            "events",
            $"fields id,name,start_time,games; where games = {gameId}; limit 200;",
            ct);

        static bool LooksLikeAward(string s)
        {
            var n = s.ToLowerInvariant();
            // Basit bir sezgisel filtre: award/bafta/d.i.c.e/golden joystick gibi
            return n.Contains("award")
                || n.Contains("bafta")
                || n.Contains("d.i.c.e")
                || n.Contains("golden joystick")
                || n.Contains("gdc awards");
        }

        static string YearSuffix(long? epoch)
        {
            if (epoch is null) return "";
            var y = DateTimeOffset.FromUnixTimeSeconds(epoch.Value).UtcDateTime.Year;
            return $" ({y})";
        }

        return rows
            .Where(e => !string.IsNullOrWhiteSpace(e.name) && LooksLikeAward(e.name!))
            .OrderBy(e => e.start_time ?? 0)
            .Select(e => $"{e.name}{YearSuffix(e.start_time)}")
            .Distinct()
            .ToList();
    }

    private sealed class EventRow
    {
        public long id { get; set; }
        public string? name { get; set; }
        public long? start_time { get; set; }
        public long[]? games { get; set; }
    }

    private async Task<List<string>> ResolveContentWarningsAsync(long[]? ageRatingIds, CancellationToken ct)
    {
        var list = new List<string>();
        if (ageRatingIds is not { Length: > 0 }) return list;

        // age_ratings → content_descriptions[]
        var ars = await PostAsync<AgeRatingWithContentRow>(
            "age_ratings",
            $"fields id,content_descriptions; where id = ({string.Join(',', ageRatingIds.Distinct())}); limit 500;",
            ct);

        var contentIds = ars.Where(a => a.content_descriptions is { Length: > 0 })
                            .SelectMany(a => a.content_descriptions!)
                            .Distinct()
                            .ToArray();
        if (contentIds.Length == 0) return list;

        // age_rating_content_descriptions → description
        var cds = await PostAsync<ContentDescRow>(
            "age_rating_content_descriptions",
            $"fields id,description; where id = ({string.Join(',', contentIds)}); limit 500;",
            ct);

        list.AddRange(cds.Select(c => c.description).Where(d => !string.IsNullOrWhiteSpace(d))!);
        return list.Distinct().ToList();
    }

    // --- modeller ---
    private sealed class LangSupportRow { public long? language { get; set; } public long? language_support_type { get; set; } }
    private sealed class AgeRatingWithContentRow { public long id { get; set; } public long[]? content_descriptions { get; set; } }
    private sealed class ContentDescRow { public long id { get; set; } public string? description { get; set; } }




    public async Task<IgdbPagedSimpleNames> GetGameAdditionsAsync(long id, CancellationToken ct = default)
    {
        // IGDB'de expansions/dlcs için version_parent = id filtrelenebilir
        var rows = await PostAsync<GameRow>(
            "games",
            $"fields id,name; where version_parent = {id}; limit 100;",
            ct);
        return new IgdbPagedSimpleNames
        {
            Results = rows.Select(r => new IgdbSimpleName { Id = r.id, Name = r.name ?? $"game-{r.id}" }).ToList()
        };
    }

    // ------------------------- Resolve helpers -------------------------
    private async Task<List<string>> ResolveNames(string endpoint, IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToArray();
        if (list.Length == 0) return new List<string>();
        var rows = await PostAsync<IdNameRow>(endpoint, $"fields id,name; where id = ({string.Join(',', list)}); limit 500;", ct);
        return rows.OrderBy(r => Array.IndexOf(list, r.id)).Select(r => r.name ?? $"{endpoint}-{r.id}").ToList();
    }

    private async Task<(List<string> developers, List<string> publishers)> ResolveCompaniesAsync(long[]? involvedCompanyIds, CancellationToken ct)
    {
        var devs = new List<string>();
        var pubs = new List<string>();
        if (involvedCompanyIds is not { Length: > 0 }) return (devs, pubs);

        var inv = await PostAsync<InvolvedCompanyRow>(
            "involved_companies",
            $"fields id,company,developer,publisher; where id = ({string.Join(',', involvedCompanyIds)}); limit 500;",
            ct);
        var companyIds = inv.Where(i => i.company.HasValue).Select(i => i.company!.Value).Distinct().ToArray();
        if (companyIds.Length == 0) return (devs, pubs);

        var comps = await PostAsync<IdNameRow>("companies", $"fields id,name; where id = ({string.Join(',', companyIds)}); limit 500;", ct);
        var map = comps.ToDictionary(c => c.id, c => c.name ?? $"company-{c.id}");

        foreach (var i in inv)
        {
            if (i.company is null) continue;
            if (i.developer == true && map.TryGetValue(i.company.Value, out var dn)) devs.Add(dn);
            if (i.publisher == true && map.TryGetValue(i.company.Value, out var pn)) pubs.Add(pn);
        }
        return (devs.Distinct().ToList(), pubs.Distinct().ToList());
    }

    private async Task<List<string>> ResolveAgeRatingsAsync(long[]? ageRatingIds, CancellationToken ct)
    {
        var names = new List<string>();
        if (ageRatingIds is not { Length: > 0 }) return names;
        var ars = await PostAsync<AgeRatingRow>("age_ratings", $"fields id,category,rating; where id = ({string.Join(',', ageRatingIds)}); limit 500;", ct);
        foreach (var a in ars)
        {
            // category: 1=ESRB, 2=PEGI, ... ; rating: enum kodu
            var label = a.category switch
            {
                1 => $"ESRB {ToEsrb(a.rating)}",
                2 => $"PEGI {ToPegi(a.rating)}",
                _ => $"Rating {a.rating}"
            };
            names.Add(label);
        }
        return names.Distinct().ToList();
    }

    private async Task<List<string>> ResolveKeywordsAsync(long[] ids, CancellationToken ct)
    {
        var rows = await PostAsync<IdNameRow>("keywords", $"fields id,name; where id = ({string.Join(',', ids.Distinct())}); limit 500;", ct);
        return rows.Select(r => r.name ?? $"kw-{r.id}").Distinct().ToList();
    }

    // ------------------------- Mappers & Models -------------------------
    private static DateTime? FromUnix(long? seconds)
    { return seconds is null ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime; }

    private static string ToEsrb(long? code) => code switch
    {
        6 => "RP",
        7 => "EC",
        8 => "E",
        9 => "E10+",
        10 => "T",
        11 => "M",
        12 => "AO",
        _ => code?.ToString() ?? "?"
    };
    private static string ToPegi(long? code) => code switch
    {
        1 => "3",
        2 => "7",
        3 => "12",
        4 => "16",
        5 => "18",
        6 => "RP",
        _ => code?.ToString() ?? "?"
    };


    public async Task<IgdbTimeToBeat?> GetTimeToBeatAsync(long gameId, CancellationToken ct = default)
    {
        // 1) Önce doğrudan bu id için dene
        var ttb = await QueryTtbAsync(gameId, ct);
        if (HasAnyValue(ttb)) return ttb;

        // 2) Eğer boş/eksik ise version_parent’ı bul
        var parent = await PostAsync<GameParentRow>(
            "games",
            $"fields version_parent; where id = {gameId}; limit 1;",
            ct);

        var parentId = parent.FirstOrDefault()?.version_parent;
        if (parentId is null) return ttb; // parent yoksa eldekini döndür (null olabilir)

        // 3) Parent için de dene
        var ttbParent = await QueryTtbAsync(parentId.Value, ct);
        return HasAnyValue(ttbParent) ? ttbParent : ttb;
    }

    private async Task<IgdbTimeToBeat?> QueryTtbAsync(long id, CancellationToken ct)
    {
        var rows = await PostAsync<TimeToBeatRow>(
            "game_time_to_beats",
            $"fields game_id,hastily,normally,completely,count; where game_id = {id}; limit 1;",
            ct);

        var r = rows.FirstOrDefault();
        if (r is null) return null;

        return new IgdbTimeToBeat
        {
            GameId = r.game_id,
            Hastily = r.hastily,       // saniye
            Normally = r.normally,     // saniye
            Completely = r.completely, // saniye
            Count = r.count
        };
    }

    private static bool HasAnyValue(IgdbTimeToBeat? t)
    => t is not null && (t.Hastily.HasValue || t.Normally.HasValue || t.Completely.HasValue);


    private sealed class TimeToBeatRow
    {
        public long game_id { get; set; }
        public int? hastily { get; set; }
        public int? normally { get; set; }
        public int? completely { get; set; }
        public int? count { get; set; }
    }


    private sealed class GameParentRow { public long? version_parent { get; set; } }
    private sealed class GameRow
    {
        public long id { get; set; }
        public string? name { get; set; }
        public string? summary { get; set; }
        public long? first_release_date { get; set; }
        public long[]? genres { get; set; }
        public long[]? platforms { get; set; }
        public long? cover { get; set; }
        public long[]? age_ratings { get; set; }
        public long[]? keywords { get; set; }
        public long[]? involved_companies { get; set; }
        public long[]? game_engines { get; set; }
    }
    private sealed class IdNameRow { public long id { get; set; } public string? name { get; set; } }
    private sealed class CoverRow { public long id { get; set; } public string? image_id { get; set; } }
    private sealed class InvolvedCompanyRow
{
    public long id { get; set; }
    public long? company { get; set; }
    public bool? developer { get; set; }
    public bool? publisher { get; set; }
    public bool? supporting { get; set; } // <-- ekle
    public bool? porting { get; set; }    // <-- opsiyonel (istersen)
    public long? game { get; set; }       // <-- opsiyonel (sorguda kullanıyorsun)
}
    private sealed class AgeRatingRow { public long id { get; set; } public long? category { get; set; } public long? rating { get; set; } }


    private static StoreLink? MakeStoreLinkFromUrl(string raw, bool includeAmazon)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();

        bool isStoreHost = StoreHosts.Any(h => host.Contains(h)) || (includeAmazon && host.Contains("amazon."));
        if (!isStoreHost) return null; // sadece mağaza

        // Steam
        if (host.Contains("steampowered.com"))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/app/(\d+)", RegexOptions.IgnoreCase);
            return new StoreLink
            {
                Store = "Steam",
                Slug = "steam",
                Domain = "store.steampowered.com",
                Url = $"https://store.steampowered.com/app/{(m.Success ? m.Groups[1].Value : "").TrimEnd('/')}",
                ExternalId = m.Success ? m.Groups[1].Value : null
            };
        }

        // Epic Games Store (hem eski /product hem yeni /p yollarını destekle)
        if (host.Contains("epicgames.com"))
        {
            var m1 = Regex.Match(uri.AbsolutePath, @"/p/([^/?#]+)", RegexOptions.IgnoreCase);
            var m2 = Regex.Match(uri.AbsolutePath, @"/product/([^/?#]+)", RegexOptions.IgnoreCase);
            var slug = m1.Success ? m1.Groups[1].Value : (m2.Success ? m2.Groups[1].Value : null);
            return new StoreLink
            {
                Store = "Epic Games Store",
                Slug = "epic-games",
                Domain = "store.epicgames.com",
                Url = slug != null ? $"https://store.epicgames.com/p/{slug}" : raw,
                ExternalId = slug
            };
        }

        // GOG
        if (host.Contains("gog.com"))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/game/([^/?#]+)", RegexOptions.IgnoreCase);
            return new StoreLink
            {
                Store = "GOG",
                Slug = "gog",
                Domain = "www.gog.com",
                Url = raw,
                ExternalId = m.Success ? m.Groups[1].Value : null
            };
        }

        // PlayStation Store (concept/product)
        if (host.Contains("playstation.com"))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/(concept|product)/([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            return new StoreLink
            {
                Store = "PlayStation Store",
                Slug = "playstation-store",
                Domain = "store.playstation.com",
                Url = raw,
                ExternalId = m.Success ? m.Groups[2].Value : null
            };
        }

        // Xbox / Microsoft Store
        if (host.Contains("xbox.com") || host.Contains("microsoft.com"))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/store/[^/]+/[^/]+/([A-Z0-9]{10,})", RegexOptions.IgnoreCase);
            var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var pid = m.Success ? m.Groups[1].Value : (q["productId"] ?? q["ProductId"]);
            return new StoreLink
            {
                Store = "Xbox Store",
                Slug = "xbox-store",
                Domain = host,
                Url = raw,
                ExternalId = string.IsNullOrWhiteSpace(pid) ? null : pid
            };
        }

        // Nintendo eShop (ülke alt alanları çok değişken; ExternalId yoksa boş geç)
        if (host.Contains("nintendo"))
        {
            return new StoreLink
            {
                Store = "Nintendo eShop",
                Slug = "nintendo-eshop",
                Domain = host,
                Url = raw
            };
        }

        // Apple App Store
        if (host.Contains("apps.apple.com"))
        {
            var m = Regex.Match(raw, @"id(\d+)");
            return new StoreLink
            {
                Store = "App Store",
                Slug = "app-store",
                Domain = "apps.apple.com",
                Url = raw,
                ExternalId = m.Success ? m.Groups[1].Value : null
            };
        }

        // Google Play
        if (host.Contains("play.google.com"))
        {
            var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var id = q["id"];
            return new StoreLink
            {
                Store = "Google Play",
                Slug = "google-play",
                Domain = "play.google.com",
                Url = raw,
                ExternalId = string.IsNullOrWhiteSpace(id) ? null : id
            };
        }

        // itch.io
        if (host.EndsWith("itch.io"))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/([^/?#]+)/?$");
            return new StoreLink
            {
                Store = "itch.io",
                Slug = "itch-io",
                Domain = host,
                Url = raw,
                ExternalId = m.Success ? m.Groups[1].Value : null
            };
        }

        // Humble
        if (host.Contains("humblebundle.com"))
        {
            return new StoreLink
            {
                Store = "Humble Store",
                Slug = "humble",
                Domain = "www.humblebundle.com",
                Url = raw
            };
        }

        // Amazon (opsiyonel)
        if (includeAmazon && host.Contains("amazon."))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/dp/([A-Z0-9]{8,})", RegexOptions.IgnoreCase);
            return new StoreLink
            {
                Store = "Amazon",
                Slug = "amazon",
                Domain = host,
                Url = raw,
                ExternalId = m.Success ? m.Groups[1].Value : null
            };
        }

        return null; // bilinmeyen host → alma
    }

    private static string GuessStoreName(string host)
    {
        if (host.Contains("store.")) return host.Replace("store.", "", StringComparison.OrdinalIgnoreCase);
        return host;
    }
    private static string SlugifyHost(string host) => host.Replace(".", "-");

    // --- IGDB row modelleri ---
    private sealed class WebsiteRow { public long id { get; set; } public long? category { get; set; } public string? url { get; set; } }
    private sealed class ExternalGameRow { public long id { get; set; } public long? category { get; set; } public string? url { get; set; } public string? uid { get; set; } }

    // ---- YALNIZ MAĞAZA ALLOW-LIST ----
    private static readonly string[] StoreHosts =
    {
    "steampowered.com",
    "epicgames.com",
    "gog.com",
    "playstation.com",
    "xbox.com",
    "microsoft.com",
    "nintendo",              // *.nintendo.*
    "apps.apple.com",
    "play.google.com",
    "itch.io",
    "humblebundle.com"
    // "amazon."  // isteğe bağlı
};

    private static readonly Regex _awardRx = new(
        "(\\baward\\b|\\bawards\\b|bafta|golden joystick|d\\.i\\.c\\.e|dice awards|game developers choice|the game awards|spike video game awards)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static List<string> ExtractAwardsFromTags(List<string> tags) =>
        tags.Where(t => !string.IsNullOrWhiteSpace(t) && _awardRx.IsMatch(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();



    public async Task<(List<string> cast, List<string> crew)> GetCreditsAsync(long gameId, CancellationToken ct = default)
{
    // ---- CREW: involved_companies -> companies ----
    var inv = await PostAsync<InvolvedCompanyRow>(
    "involved_companies",
    $"fields id,company,developer,publisher,supporting,porting,game; where game = {gameId}; limit 200;",
    ct);


    var companyIds = inv.Where(i => i.company.HasValue).Select(i => i.company!.Value).Distinct().ToArray();
    var nameById = new Dictionary<long, string>();
    if (companyIds.Length > 0)
    {
        var comps = await PostAsync<IdNameRow>(
            "companies",
            $"fields id,name; where id = ({string.Join(',', companyIds)}); limit 500;",
            ct);
        nameById = comps.ToDictionary(c => c.id, c => c.name ?? $"company-{c.id}");
    }

    var crew = new List<string>();
    foreach (var i in inv)
    {
        if (i.company is null) continue;
        var n = nameById.TryGetValue(i.company.Value, out var nm) ? nm : $"company-{i.company}";
        var role = i.developer == true ? "Developer"
                 : i.publisher == true ? "Publisher"
                 : i.supporting == true ? "Support"
                 : "Company";
        crew.Add($"{n} ({role})");
    }
    crew = crew.Distinct().ToList();

    // ---- CAST (voice actors): characters -> people ----
    // Bu ilişki her oyunda dolu olmayabilir; boş gelirse problem değil.
    var cast = new List<string>();
    try
    {
        var chars = await PostAsync<CharacterRow>(
            "characters",
            $"fields id,name,people,games; where games = ({gameId}); limit 500;",
            ct);

        var peopleIds = chars.Where(c => c.people is { Length: > 0 })
                             .SelectMany(c => c.people!)
                             .Distinct()
                             .ToArray();

        if (peopleIds.Length > 0)
        {
            var people = await PostAsync<PersonRow>(
                "people",
                $"fields id,name; where id = ({string.Join(',', peopleIds)}); limit 500;",
                ct);
            cast = people.Select(p => p.name ?? $"person-{p.id}")
                         .Distinct()
                         .ToList();
        }
    }
    catch
    {
        // characters/people endpoint’leri veri yoksa ya da model farklıysa boş bırak.
    }

    return (cast, crew);
}

// MODELLERE ekle:
private sealed class CharacterRow { public long id { get; set; } public string? name { get; set; } public long[]? people { get; set; } }
private sealed class PersonRow    { public long id { get; set; } public string? name { get; set; } }


private sealed class CreditRow
{
    public long id { get; set; }
    public long? person { get; set; }
    public long? character { get; set; }
    public bool? voice_actor { get; set; } // opsiyonel, gerekirse kullanırız
}

    

}




