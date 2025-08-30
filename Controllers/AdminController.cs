using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Globalization;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Game> _games;
    private readonly IMongoCollection<Game_Details> _details;
    private readonly IMongoCollection<Genre> _genres;

    private readonly IMongoCollection<Platform> _platforms;

    private readonly IMongoCollection<MinRequirement> _minReqs;
private readonly IMongoCollection<RecRequirement> _recReqs;
    private readonly IConfiguration _config;

    public AdminController(MongoDbService service, IConfiguration config)
    {
        var db = service?.Database
                ?? throw new InvalidOperationException("MongoDbService.database is null.");

        var usersCollectionName = config["MongoDb:UsersCollection"] ?? "User";

        _games = db.GetCollection<Game>("Games");
        _details = db.GetCollection<Game_Details>("GameDetails");
        _genres = db.GetCollection<Genre>("Genres");
        _platforms = db.GetCollection<Platform>("Platforms");
        _minReqs = db.GetCollection<MinRequirement>("MinRequirements");
        _recReqs = db.GetCollection<RecRequirement>("RecRequirements");
        _users = db.GetCollection<User>(usersCollectionName);
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin")]
    public IActionResult Dashboard() => Ok("Admin panel verisi 🔐");

    [HttpGet("usergamecount")]
    [Authorize] // istersen Roles="Admin"
    public async Task<IActionResult> UserGameCount([FromQuery] int windowDays = 7, CancellationToken ct = default)
    {
        if (windowDays <= 0) windowDays = 7;

        // 1) Toplamlar
        var totalUsers = await _users.CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: ct);
        var totalGames = await _games.CountDocumentsAsync(FilterDefinition<Game>.Empty, cancellationToken: ct);

        // 2) Tarih pencereleri (UTC gün)
        var todayUtc = DateTime.UtcNow.Date;
        var currEnd = todayUtc.AddDays(1);                 // [currStart, currEnd)
        var currStart = currEnd.AddDays(-windowDays);

        var prevEnd = currStart;                           // [prevStart, prevEnd)
        var prevStart = prevEnd.AddDays(-windowDays);

        // 3) Sayımlar
        var (uCurr, uPrev) = await CountWindowPairAsync(_users, currStart, currEnd, prevStart, prevEnd, ct);
        var (gCurr, gPrev) = await CountWindowPairAsync(_games, currStart, currEnd, prevStart, prevEnd, ct);

        // 4) Yüzde fark
        static double DeltaPct(long curr, long prev)
            => prev <= 0 ? (curr > 0 ? 100d : 0d) : ((curr - prev) * 100.0 / prev);

        var usersDelta = DeltaPct(uCurr, uPrev);
        var gamesDelta = DeltaPct(gCurr, gPrev);

        return Ok(new
        {
            totalUsers,
            totalGames,
            windowDays,
            users = new { current = uCurr, previous = uPrev, deltaPct = usersDelta },
            games = new { current = gCurr, previous = gPrev, deltaPct = gamesDelta }
        });
    }

    // ---------- yardımcılar ----------

    // Belgedeki "oluşturulma" tarihi için coalesce: Createdat > CreatedAt > CreatedDate > createdAt > ObjectId timestamp
    private static BsonDocument CoalesceCreatedExpr() =>
        new BsonDocument("$ifNull", new BsonArray {
        "$Createdat",
        new BsonDocument("$ifNull", new BsonArray {
            "$CreatedAt",
            new BsonDocument("$ifNull", new BsonArray {
                "$CreatedDate",
                new BsonDocument("$ifNull", new BsonArray {
                    "$createdAt",
                    new BsonDocument("$toDate", "$_id")
                })
            })
        })
        });

    private static async Task<long> CountInRangeAsync<TDoc>(IMongoCollection<TDoc> col, DateTime startInc, DateTime endExc, CancellationToken ct)
    {
        var createdExpr = CoalesceCreatedExpr();

        var pipeline = new List<BsonDocument>
    {
        new BsonDocument("$addFields", new BsonDocument("_created", createdExpr)),
        new BsonDocument("$match", new BsonDocument("_created", new BsonDocument {
            { "$gte", startInc }, { "$lt", endExc }
        })),
        new BsonDocument("$count", "c")
    };

        var list = await col.Database.GetCollection<BsonDocument>(col.CollectionNamespace.CollectionName)
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .ToListAsync(ct);

        return list.Count > 0 ? list[0].GetValue("c", 0).ToInt64() : 0L;
    }

    private static async Task<(long curr, long prev)> CountWindowPairAsync<TDoc>(
        IMongoCollection<TDoc> col,
        DateTime currStart, DateTime currEnd,
        DateTime prevStart, DateTime prevEnd,
        CancellationToken ct)
    {
        var curr = await CountInRangeAsync(col, currStart, currEnd, ct);
        var prev = await CountInRangeAsync(col, prevStart, prevEnd, ct);
        return (curr, prev);
    }

    // ====== YENİ: /api/admin/growth ======
    /// <summary>
    /// GET /api/admin/growth?from=2025-08-01&to=2025-08-25&mode=daily|cumulative
    /// Dönüş: [{ date: 'yyyy-MM-dd', users: n, games: n }]
    /// </summary>
    [HttpGet("growth")]
    [Authorize] // istersen Roles="Admin" yapabilirsin
    public async Task<IActionResult> Growth([FromQuery] string from, [FromQuery] string to, [FromQuery] string? mode = "cumulative", CancellationToken ct = default)
    {
        if (!TryParseDay(from, out var fromDay) || !TryParseDay(to, out var toDay))
            return BadRequest(new { message = "from/to 'yyyy-MM-dd' formatında olmalı." });

        // aralık: [from 00:00, to 23:59:59.999] → toExclusive = to + 1 gün
        var start = fromDay.Date;
        var endExclusive = toDay.Date.AddDays(1);

        // Users için günlük sayım
        var usersDaily = await AggregateDailyCountsAsync(_users, start, endExclusive, ct);

        // Games için günlük sayım
        var gamesDaily = await AggregateDailyCountsAsync(_games, start, endExclusive, ct);

        // Gün listesini doldur & birleştir
        var allDays = EnumerateDays(start, endExclusive.AddDays(-1)).Select(d => d.ToString("yyyy-MM-dd")).ToList();
        var mapU = usersDaily.ToDictionary(x => x.day, x => x.count);
        var mapG = gamesDaily.ToDictionary(x => x.day, x => x.count);

        var list = new List<(string day, int u, int g)>(allDays.Count);
        foreach (var d in allDays)
        {
            mapU.TryGetValue(d, out var u);
            mapG.TryGetValue(d, out var g);
            list.Add((d, u, g));
        }

        // Mode: daily or cumulative
        var modeNorm = (mode ?? "cumulative").Trim().ToLowerInvariant();
        if (modeNorm == "cumulative")
        {
            int cu = 0, cg = 0;
            list = list.Select(x => { cu += x.u; cg += x.g; return (x.day, cu, cg); }).ToList();
        }

        var payload = list.Select(x => new { date = x.day, users = x.u, games = x.g }).ToList();
        return Ok(payload);
    }

    // ---------- Helpers ----------

    private static bool TryParseDay(string s, out DateTime dayUtc)
    {
        // from/to ISO gün (yyyy-MM-dd) bekleniyor; zaman bilgisi verilirse da kabul edelim
        // Her halükarda UTC güne yuvarlıyoruz.
        if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d1))
        { dayUtc = d1.Date; return true; }
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d2))
        { dayUtc = d2.Date; return true; }
        dayUtc = default;
        return false;
    }

    private static IEnumerable<DateTime> EnumerateDays(DateTime startInclusive, DateTime endInclusive)
    {
        for (var d = startInclusive.Date; d <= endInclusive.Date; d = d.AddDays(1))
            yield return d;
    }

    /// <summary>
    /// Koleksiyon için günlük sayım döndürür. Tarih alanı coalesce:
    /// Createdat > CreatedAt > CreatedDate > createdAt > ObjectId timestamp
    /// </summary>
    private static async Task<List<(string day, int count)>> AggregateDailyCountsAsync<TDoc>(
        IMongoCollection<TDoc> col,
        DateTime startInclusive,
        DateTime endExclusive,
        CancellationToken ct)
    {
        // $addFields: _created = coalesce(...)
        var coalesceCreated = new BsonDocument("$ifNull", new BsonArray {
            "$Createdat",
            new BsonDocument("$ifNull", new BsonArray {
                "$CreatedAt",
                new BsonDocument("$ifNull", new BsonArray {
                    "$CreatedDate",
                    new BsonDocument("$ifNull", new BsonArray {
                        "$createdAt",
                        new BsonDocument("$toDate", "$_id") // ObjectId → Date
                    })
                })
            })
        });

        var pipeline = new List<BsonDocument>
        {
            new BsonDocument("$addFields", new BsonDocument("_created", coalesceCreated)),
            new BsonDocument("$match", new BsonDocument {
                { "_created", new BsonDocument {
                    { "$gte", startInclusive },
                    { "$lt",  endExclusive }
                }}
            }),
            new BsonDocument("$group", new BsonDocument {
                { "_id", new BsonDocument("$dateToString", new BsonDocument {
                    { "format", "%Y-%m-%d" },
                    { "date", "$_created" }
                }) },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$project", new BsonDocument {
                { "_id", 0 },
                { "day", "$_id" },
                { "count", 1 }
            }),
            new BsonDocument("$sort", new BsonDocument("day", 1))
        };

        var cursor = await col.Database
            .GetCollection<BsonDocument>(col.CollectionNamespace.CollectionName)
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .ToListAsync(ct);

        var result = new List<(string day, int count)>(cursor.Count);
        foreach (var doc in cursor)
        {
            var day = doc.GetValue("day", BsonNull.Value)?.AsString ?? "";
            var cnt = doc.GetValue("count", BsonNull.Value)?.ToInt32() ?? 0;
            if (!string.IsNullOrEmpty(day))
                result.Add((day, cnt));
        }
        return result;
    }


    [HttpGet("games")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<GameListItemDto>>> GetGamesAsync(
      [FromQuery] int skip = 0,
      [FromQuery] int take = 50,
      [FromQuery] string? q = null)
    {
        if (skip < 0) skip = 0;
        if (take <= 0 || take > 200) take = 50;

        var filter = Builders<Game>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var regex = new BsonRegularExpression(q, "i");
            filter = Builders<Game>.Filter.Or(
                Builders<Game>.Filter.Regex(g => g.Game_Name, regex),
                Builders<Game>.Filter.Regex(g => g.Studio, regex)
            );
        }

        var games = await _games.Find(filter)
            .SortByDescending(g => g.Createdat)
            .Skip(skip)
            .Limit(take)
            .ToListAsync();

        var gameIds = games.Select(g => g.Id).ToList();

        var details = await _details
            .Find(d => gameIds.Contains(d.GameId))
            .ToListAsync();

        // ---- GENRES
        var allGenreIds = details.Where(d => d.GenreIds != null)
                                 .SelectMany(d => d.GenreIds!)
                                 .Distinct()
                                 .ToList();

        var genreDict = new Dictionary<string, string>();
        if (allGenreIds.Count > 0)
        {
            var genres = await _genres.Find(g => allGenreIds.Contains(g.Id)).ToListAsync();
            foreach (var gr in genres)
                genreDict[gr.Id] = gr.Name; // Modelinde isim alanı farklıysa burayı uyarlayın
        }

        // ---- PLATFORMS (YENİ)
        var allPlatformIds = details.Where(d => d.PlatformIds != null)
                                    .SelectMany(d => d.PlatformIds!)
                                    .Distinct()
                                    .ToList();

        var platformDict = new Dictionary<string, string>();
        if (allPlatformIds.Count > 0)
        {
            var plats = await _platforms.Find(p => allPlatformIds.Contains(p.Id)).ToListAsync();
            foreach (var p in plats)
                platformDict[p.Id] = p.Name; // Platform modelinizde ad alanı farklıysa uyarlayın (örn: p.Platform_Name)
        }

        var dto = games.Select(g =>
        {
            var det = details.FirstOrDefault(d => d.GameId == g.Id);

            var genreNames = new List<string>();
            if (det?.GenreIds != null)
            {
                foreach (var gid in det.GenreIds)
                    if (gid != null && genreDict.TryGetValue(gid, out var name))
                        genreNames.Add(name);
            }

            // YENİ: platform adları
            var platformNames = new List<string>();
            if (det?.PlatformIds != null)
            {
                foreach (var pid in det.PlatformIds)
                    if (pid != null && platformDict.TryGetValue(pid, out var pname))
                        platformNames.Add(pname);
            }

            return new GameListItemDto
            {
                Id = g.Id,
                Cover = g.Main_image_URL,
                Title = g.Game_Name,
                Release = g.Release_Date,
                Developer = det?.Developer ?? g.Studio, // developer öncelik
                Genres = genreNames,
                Platforms = platformNames,              // YENİ
                Story = det?.Story
            };
        })
        .ToList();

        return Ok(dto);
    }

    [HttpDelete("games/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGame(string id, CancellationToken ct)
    {
        var game = await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
        if (game == null)
            return NotFound(new { message = "Game not found" });

        await _games.DeleteOneAsync(g => g.Id == id, ct);
        await _details.DeleteManyAsync(d => d.GameId == id, ct); // detayları da sil
                                                                 // gerekiyorsa genres/platforms silmeye gerek yok (onlar ortak kullanılıyor)

        return Ok(new { message = $"Game {id} deleted" });
    }


    [HttpGet("games/{id}")]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<GameDetailDto>> GetGameById(string id, CancellationToken ct)
{
    var game = await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
    if (game == null)
        return NotFound(new { message = "Game not found" });

    var details = await _details.Find(d => d.GameId == id).FirstOrDefaultAsync(ct);

    // --- GENRES ---
    var genreNames = new List<string>();
    if (details?.GenreIds != null && details.GenreIds.Count > 0)
    {
        var genres = await _genres.Find(g => details.GenreIds.Contains(g.Id)).ToListAsync(ct);
        genreNames = genres.Select(g => g.Name).ToList(); // Genre modelinde "Name" property olduğunu varsayıyorum
    }

    // --- PLATFORMS ---
    var platformNames = new List<string>();
    if (details?.PlatformIds != null && details.PlatformIds.Count > 0)
    {
        var plats = await _platforms.Find(p => details.PlatformIds.Contains(p.Id)).ToListAsync(ct);
        platformNames = plats.Select(p => p.Name).ToList(); // Platform modelinde "Name" property olduğunu varsayıyorum
    }

    string? minText = null, recText = null;
    if (!string.IsNullOrEmpty(details?.MinRequirementId))
    {
        var m = await _minReqs.Find(x => x.Id == details.MinRequirementId).FirstOrDefaultAsync(ct);
        minText = m?.Text;
    }
    if (!string.IsNullOrEmpty(details?.RecRequirementId))
    {
        var r = await _recReqs.Find(x => x.Id == details.RecRequirementId).FirstOrDefaultAsync(ct);
        recText = r?.Text;
    }


    var dto = new GameDetailDto
    {
        Id = game.Id,
        Title = game.Game_Name,
        ReleaseDate = game.Release_Date,
        Studio = game.Studio,
        GgdbRating = game.GgDb_Rating,
        MetacriticRating = game.Metacritic_Rating,
        Cover = game.Main_image_URL,
        Video = game.Main_video_URL,

        Developer = details?.Developer,
        Publisher = details?.Publisher,
        Genres = genreNames,
        Platforms = platformNames,
        Story = details?.Story,
        Tags = details?.Tags ?? new List<string>(),
        Dlcs = details?.DLCs ?? new List<string>(),
        Crew = game.Crew,
        Awards = details?.Awards,
        GameEngine = details?.Engines ?? new List<string>(),

        MinRequirements = minText,
        RecRequirements = recText,
        ContentWarnings = details?.Content_Warnings ?? new List<string>(),
        AgeRatings = details?.Age_Ratings ?? new List<string>(),
        AudioLanguages = details?.Audio_Language ?? new List<string>(),
        SubtitleLanguages = details?.Subtitles ?? new List<string>(),
        InterfaceLanguages = details?.Interface_Language ?? new List<string>(),

    StoreLinks = (details?.Store_Links ?? new List<StoreLink>())
    .Select(s => new StoreLinkDto
    {
        StoreId    = s.StoreId,
        Store      = s.Store,
        Slug       = s.Slug,
        Domain     = s.Domain,
        Url        = s.Url,
        ExternalId = s.ExternalId
    })
    .ToList()
    };

    return Ok(dto);
}

[HttpPut("games/{id}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> UpdateGame(string id, [FromBody] GameDetailDto dto, CancellationToken ct)
{
    if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
        return BadRequest(new { message = "Invalid payload: 'title' is required." });

    // ---- Game (temel) ----
    var game = await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
    if (game == null)
        return NotFound(new { message = "Game not found" });

    game.Game_Name = dto.Title;
    game.Release_Date = dto.ReleaseDate;
    game.Studio = dto.Studio;
    game.GgDb_Rating = dto.GgdbRating;
    game.Metacritic_Rating = dto.MetacriticRating;
    game.Main_image_URL = dto.Cover;
    game.Main_video_URL = dto.Video;
    game.Crew = dto.Crew ?? new List<string>();

    await _games.ReplaceOneAsync(g => g.Id == id, game, cancellationToken: ct);

    // ---- Game_Details (detay) ----
    var details = await _details.Find(d => d.GameId == id).FirstOrDefaultAsync(ct)
                  ?? new Game_Details { GameId = id };

    details.Developer = dto.Developer;
    details.Publisher = dto.Publisher;
    details.Story = dto.Story;
    details.Tags = dto.Tags ?? new List<string>();
    details.DLCs = dto.Dlcs ?? new List<string>();
    details.Awards = dto.Awards;
    details.Engines = dto.GameEngine ?? new List<string>();
    
    details.Content_Warnings = dto.ContentWarnings ?? new List<string>();
    details.Age_Ratings = dto.AgeRatings ?? new List<string>();
    
    details.Audio_Language = dto.AudioLanguages ?? new List<string>();
    details.Subtitles = dto.SubtitleLanguages ?? new List<string>();
    details.Interface_Language = dto.InterfaceLanguages ?? new List<string>();

    // ---- System Requirements (Min / Rec) — upsert + Id bağlama ----
        // dto.MinRequirements / dto.RecRequirements string (metin) kabul edildiği varsayımıyla
       if (dto.MinRequirements is { Length: >0 } minText) // null veya whitespace değil
{
    if (!string.IsNullOrEmpty(details.MinRequirementId))
    {
        var upd = Builders<MinRequirement>.Update.Set(x => x.Text, minText);
        await _minReqs.UpdateOneAsync(
            Builders<MinRequirement>.Filter.Eq(x => x.Id, details.MinRequirementId),
            upd,
            cancellationToken: ct
        );
    }
    else
    {
        var doc = new MinRequirement { Text = minText }; // _id’yi Mongo versin
        await _minReqs.InsertOneAsync(doc, cancellationToken: ct);
        details.MinRequirementId = doc.Id;
    }
}
else
{
    // referansı temizle (belgeyi silmek opsiyonel)
    if (!string.IsNullOrEmpty(details.MinRequirementId))
    {
        try { await _minReqs.DeleteOneAsync(x => x.Id == details.MinRequirementId, ct); } catch { /* yoksay */ }
    }
    details.MinRequirementId = null;
}

// REC
if (dto.RecRequirements is { Length: >0 } recText)
{
    if (!string.IsNullOrEmpty(details.RecRequirementId))
    {
        var upd = Builders<RecRequirement>.Update.Set(x => x.Text, recText);
        await _recReqs.UpdateOneAsync(
            Builders<RecRequirement>.Filter.Eq(x => x.Id, details.RecRequirementId),
            upd,
            cancellationToken: ct
        );
    }
    else
    {
        var doc = new RecRequirement { Text = recText };
        await _recReqs.InsertOneAsync(doc, cancellationToken: ct);
        details.RecRequirementId = doc.Id;
    }
}
else
{
    if (!string.IsNullOrEmpty(details.RecRequirementId))
    {
        try { await _recReqs.DeleteOneAsync(x => x.Id == details.RecRequirementId, ct); } catch { /* yoksay */ }
    }
    details.RecRequirementId = null;
}

    // ---- Genres (isim → id)
    if (dto.Genres != null)
    {
        var genreDocs = await _genres
            .Find(g => dto.Genres.Contains(g.Name))
            .Project(g => new { g.Id, g.Name })
            .ToListAsync(ct);

        details.GenreIds = genreDocs.Select(x => x.Id).ToList();
    }

    // ---- Platforms (isim → id)
    if (dto.Platforms != null)
    {
        var platformDocs = await _platforms
            .Find(p => dto.Platforms.Contains(p.Name))
            .Project(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        details.PlatformIds = platformDocs.Select(x => x.Id).ToList();
        
    }

 details.Store_Links = (dto.StoreLinks ?? new List<StoreLinkDto>())
    .Where(s => !string.IsNullOrWhiteSpace(s.Url))
    .Select(s => new StoreLink
    {
        StoreId    = s.StoreId ?? 0,
        Store      = s.Store      ?? "",
        Slug       = s.Slug       ?? "",
        Domain     = s.Domain     ?? "",
        Url        = s.Url        ?? "",
        ExternalId = s.ExternalId
    })
    .ToList();
    await _details.ReplaceOneAsync(d => d.GameId == id, details, new ReplaceOptions { IsUpsert = true }, ct);

    return Ok(new { message = $"Game {id} updated" });
}



}



