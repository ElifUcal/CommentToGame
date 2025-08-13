using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.DTOs;
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
            $"fields id,name,first_release_date,genres,platforms,cover,age_ratings,keywords,involved_companies; " +
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
        // name ~ "text*" (case-insensitive)
        var safe = query.Replace("\"", "\\\"");
        var games = await PostAsync<GameRow>(
            "games",
            $"fields id,name,first_release_date,genres,platforms,cover,age_ratings,keywords,involved_companies; " +
            $"where name ~ \"{safe}\"*; limit {pageSize}; offset {offset};",
            ct);

        return new IgdbPagedGames
        {
            Results = games.Select(g => new IgdbGameCard { Id = g.id, Name = g.name ?? $"game-{g.id}" }).ToList(),
            Next = games.Length < pageSize ? null : "next"
        };
    }

    public async Task<IgdbGameDetail?> GetGameDetailAsync(long id, CancellationToken ct = default)
    {
        var rows = await PostAsync<GameRow>(
            "games",
            $"fields id,name,summary,first_release_date,genres,platforms,cover,age_ratings,keywords,involved_companies; where id = {id}; limit 1;",
            ct);
        var g = rows.FirstOrDefault();
        if (g is null) return null;

        // Resolve lookups
        var genreNames = g.genres is { Length: > 0 } ? await ResolveNames("genres", g.genres!, ct) : new List<string>();
        var platformNames = g.platforms is { Length: > 0 } ? await ResolveNames("platforms", g.platforms!, ct) : new List<string>();

        var (developers, publishers) = await ResolveCompaniesAsync(g.involved_companies, ct);
        var ageNames = await ResolveAgeRatingsAsync(g.age_ratings, ct);
        var tagNames = g.keywords is { Length: > 0 } ? await ResolveKeywordsAsync(g.keywords!, ct) : new List<string>();

        string? coverUrl = null;
        if (g.cover.HasValue)
        {
            var cover = await PostAsync<CoverRow>("covers", $"fields image_id; where id = {g.cover.Value}; limit 1;", ct);
            var img = cover.FirstOrDefault()?.image_id;
            if (!string.IsNullOrWhiteSpace(img))
                coverUrl = $"https://images.igdb.com/igdb/image/upload/t_cover_big/{img}.jpg";
        }

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
            Tags = tagNames
        };
    }

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
    }
    private sealed class IdNameRow { public long id { get; set; } public string? name { get; set; } }
    private sealed class CoverRow { public long id { get; set; } public string? image_id { get; set; } }
    private sealed class InvolvedCompanyRow { public long id { get; set; } public long? company { get; set; } public bool? developer { get; set; } public bool? publisher { get; set; } }
    private sealed class AgeRatingRow { public long id { get; set; } public long? category { get; set; } public long? rating { get; set; } }
}
