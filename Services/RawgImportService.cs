using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.Models;
using MongoDB.Driver;
using MongoDB.Bson; // <-- ekle


namespace CommentToGame.Services;

public class RawgImportService
{
    private readonly IRawgClient _rawg;
    private readonly IMongoCollection<Game> _games;
    private readonly IMongoCollection<Game_Details> _details;
    private readonly IMongoCollection<Genre> _genres;
    private readonly IMongoCollection<Platform> _platforms;

    public RawgImportService(IRawgClient rawg, MongoDbService svc)
    {
        _rawg = rawg;
        var db = svc.Database!;
        _games = db.GetCollection<Game>("Games");
        _details = db.GetCollection<Game_Details>("GameDetails");
        _genres = db.GetCollection<Genre>("Genres");
        _platforms = db.GetCollection<Platform>("Platforms");
    }

    public async Task<int> ImportAsync(int pages = 1, int pageSize = 40)
    {
        int count = 0;

        for (int page = 1; page <= pages; page++)
        {
            var list = await _rawg.GetGamesAsync(page, pageSize);

            foreach (var g in list.Results)
            {
                var detail = await _rawg.GetGameDetailAsync(g.Id);

                // Genres upsert
                var genreIds = new List<string>();
                foreach (var genre in detail.Genres)
                {
                    var filter = Builders<Genre>.Filter.Eq(x => x.Name, genre.Name);
                    var update = Builders<Genre>.Update.SetOnInsert(x => x.Name, genre.Name);
                    var opts = new FindOneAndUpdateOptions<Genre>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    };
                    var upserted = await _genres.FindOneAndUpdateAsync(filter, update, opts);
                    genreIds.Add(upserted.Id);
                }

                // Platforms upsert
                var platformIds = new List<string>();
                foreach (var plat in detail.Platforms)
                {
                    var filter = Builders<Platform>.Filter.Eq(x => x.Name, plat.Platform.Name);
                    var update = Builders<Platform>.Update.SetOnInsert(x => x.Name, plat.Platform.Name);
                    var opts = new FindOneAndUpdateOptions<Platform>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    };
                    var upserted = await _platforms.FindOneAndUpdateAsync(filter, update, opts);
                    platformIds.Add(upserted.Id);
                }

                // Game insert/update
                var game = new Game
                {
                    Game_Name = detail.Name,
                    Release_Date = DateTime.TryParse(detail.Released, out var dt) ? dt : null,
                    Metacritic_Rating = detail.Metacritic,
                    GgDb_Rating = detail.Rating.HasValue ? (int)(detail.Rating * 20) : null, // RAWG rating /5 → /100
                    CompanyIds = new List<string>(), // RAWG'den şirketleri çekmek istersen burada ekle
                    Main_image_URL = null // RAWG'den resim linkini ekleyebilirsin
                };

                var gFilter = Builders<Game>.Filter.Eq(x => x.Game_Name, game.Game_Name);
                await _games.ReplaceOneAsync(gFilter, game, new ReplaceOptions { IsUpsert = true });

                var gameFromDb = await _games.Find(gFilter).FirstOrDefaultAsync();

                // Game_Details insert/update
                var details = new Game_Details
                {
                    GameId = gameFromDb.Id,
                    Developer = detail.Developers.FirstOrDefault()?.Name,
                    Publisher = detail.Publishers.FirstOrDefault()?.Name,
                    GenreIds = genreIds,
                    PlatformIds = platformIds,
                    Story = detail.DescriptionRaw
                };

                var dFilter = Builders<Game_Details>.Filter.Eq(x => x.GameId, gameFromDb.Id);
                await _details.ReplaceOneAsync(dFilter, details, new ReplaceOptions { IsUpsert = true });

                count++;
            }
        }

        return count;
    }

    public async Task<int> TestFetchAsync(CancellationToken ct = default)
    {
        var page = await _rawg.GetGamesAsync(page: 1, pageSize: 3);
        return page.Results?.Count ?? 0;
    }

    public async Task<int> ImportAllBySearchAsync(string query, int pageSize = 40, int maxPages = 5, int delayMs = 250)
    {
        int imported = 0;
        int page = 1;
        string? next;

        do
        {
            var list = await _rawg.SearchGamesAsync(query, page, pageSize);
            foreach (var s in list.Results)
            {
                var detail = await _rawg.GetGameDetailAsync(s.Id);

                // 404 veya null döndüyse atla
                if (detail == null)
                {
                    Console.WriteLine($"[RAWG] Skipped (detail not found) for id={s.Id}, name={s.Name}");
                    continue;
                }

                await UpsertOneAsync(detail);
                imported++;
            }

            next = list.Next;
            page++;

            if (delayMs > 0)
                await Task.Delay(delayMs); // rate limit için küçük mola
        }
        while (!string.IsNullOrEmpty(next) && page <= maxPages);

        return imported;
    }
    private async Task UpsertOneAsync(RawgGameDetail detail)
    {
        // 0) Koruma: isim yoksa geç
        if (string.IsNullOrWhiteSpace(detail.Name)) return;

        // 1) Genres upsert
        var genreIds = new List<string>();
        foreach (var genre in detail.Genres)
        {
            var gFilter = Builders<Genre>.Filter.Eq(x => x.Name, genre.Name);
            var gUpdate = Builders<Genre>.Update
                .SetOnInsert(x => x.Name, genre.Name)
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());
            var gUp = await _genres.FindOneAndUpdateAsync(
                gFilter, gUpdate,
                new FindOneAndUpdateOptions<Genre> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
            genreIds.Add(gUp.Id);
        }

        // 2) Platforms upsert
        var platformIds = new List<string>();
        foreach (var plat in detail.Platforms)
        {
            var name = plat.Platform.Name;
            var pFilter = Builders<Platform>.Filter.Eq(x => x.Name, name);
            var pUpdate = Builders<Platform>.Update
                .SetOnInsert(x => x.Name, name)
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());
            var pUp = await _platforms.FindOneAndUpdateAsync(
                pFilter, pUpdate,
                new FindOneAndUpdateOptions<Platform> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
            platformIds.Add(pUp.Id);
        }

        // 3) Game upsert (isim bazlı)
        var gameFilter = Builders<Game>.Filter.Eq(x => x.Game_Name, detail.Name);
    // 3) Game upsert (isim bazlı) – mevcut gameUpdate'ini genişlet
var gameUpdate = Builders<Game>.Update
    .Set(x => x.Release_Date, DateTime.TryParse(detail.Released, out var dt) ? dt : null)
    .Set(x => x.Metacritic_Rating, detail.Metacritic)
    .Set(x => x.GgDb_Rating, detail.Rating.HasValue ? (int?)Math.Round(detail.Rating.Value * 20) : null)
    .Set(x => x.Popularity, detail.Added)                   // NEW
    .Set(x => x.Main_image_URL, detail.BackgroundImage)     // NEW
    .Set(x => x.CompanyIds, new List<string>())
    .SetOnInsert(x => x.Game_Name, detail.Name)
    .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());

        var gameFromDb = await _games.FindOneAndUpdateAsync(
            gameFilter, gameUpdate,
            new FindOneAndUpdateOptions<Game> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

// 4) Game_Details upsert – About + yaş dereceleri + (opsiyonel) series + PC requirements
var detFilter = Builders<Game_Details>.Filter.Eq(x => x.GameId, gameFromDb.Id);

// Age ratings: RAWG'deki Id'leri doğrudan koleksiyondaki int listene yazıyoruz
var ageRatingNames = new List<string>();

if (!string.IsNullOrWhiteSpace(detail.EsrbRating?.Name))
    ageRatingNames.Add(detail.EsrbRating.Name);

if (detail.AgeRatings != null)
{
    ageRatingNames.AddRange(
        detail.AgeRatings
              .Select(a => a.Name)
              .Where(n => !string.IsNullOrWhiteSpace(n))!);
}

ageRatingNames = ageRatingNames.Distinct().ToList();

var detUpdate = Builders<Game_Details>.Update
    .Set(x => x.Developer, detail.Developers.FirstOrDefault()?.Name)
    .Set(x => x.Publisher, detail.Publishers.FirstOrDefault()?.Name)
    .Set(x => x.GenreIds, genreIds)
    .Set(x => x.PlatformIds, platformIds)
    .Set(x => x.Story, detail.DescriptionRaw)              // About
    .Set(x => x.Age_Ratings, ageRatingNames)                 // NEW
    .SetOnInsert(x => x.GameId, gameFromDb.Id)
    .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());

// (opsiyonel) Series: isim listesini DLCs ya da Tags içine basabiliriz
try
{
    var series = await _rawg.GetGameSeriesAsync(detail.Id);
    if (series?.Results?.Count > 0)
    {
        var names = series.Results.Select(r => r.Name).Distinct().ToList();
        detUpdate = detUpdate.Set(x => x.DLCs, names);   // Modelinde uygun alan bu olduğu için
        // alternatif: .Set(x => x.Tags, names)
    }
}
catch { /* sessiz geç: bazı oyunlarda olmayabilir */ }

// (opsiyonel) PC sistem gereksinimleri (varsa)
var pcReq = detail.Platforms?.FirstOrDefault(p => p.Platform.Slug == "pc")?.Requirements;
if (pcReq is not null && (!string.IsNullOrWhiteSpace(pcReq.Minimum) || !string.IsNullOrWhiteSpace(pcReq.Recommended)))
{
    // Eğer MinRequirement / RecRequirement diye master koleksiyonların varsa burada upsert edip
    // Id'lerini Game_Details.MinRequirementId / RecRequirementId'ye set edebilirsin.
    // Örnek (model isimlerini kendine göre güncelle):
    // var minId = await UpsertMinRequirementAsync(pcReq.Minimum);
    // var recId = await UpsertRecRequirementAsync(pcReq.Recommended);
    // detUpdate = detUpdate.Set(x => x.MinRequirementId, minId)
    //                      .Set(x => x.RecRequirementId, recId);
}

await _details.UpdateOneAsync(detFilter, detUpdate, new UpdateOptions { IsUpsert = true });
    }

// (İsteğe bağlı) Sayfalı liste
public async Task<List<Game>> GetAllGamesAsync(int skip, int take)
{
    if (skip < 0) skip = 0;
    if (take <= 0) take = 50;
    return await _games.Find(FilterDefinition<Game>.Empty)
                       .Skip(skip)
                       .Limit(take)
                       .ToListAsync();
}

// (İsteğe bağlı) DB'de isimle ara (case-insensitive)
public async Task<List<Game>> SearchGamesInDbAsync(string q, int limit = 50)
{
    if (string.IsNullOrWhiteSpace(q)) return new List<Game>();
    var filter = Builders<Game>.Filter.Regex(g => g.Game_Name, new BsonRegularExpression(q, "i"));
    return await _games.Find(filter).Limit(limit).ToListAsync();
}
public async Task<List<object>> SearchGamesWithDetailsAsync(string q, bool officialOnly = false, int limit = 50)
{
    if (string.IsNullOrWhiteSpace(q)) q = ".*";

    var pipeline = new List<BsonDocument>
    {
        // name filtre
        new BsonDocument("$match", new BsonDocument("Game_Name",
            new BsonDocument("$regex", q).Add("$options", "i"))),

        // details join
        new BsonDocument("$lookup", new BsonDocument {
            { "from", "GameDetails" },
            { "localField", "_id" },
            { "foreignField", "GameId" },
            { "as", "details" }
        }),

        // --------- YAMA #1: details hep obje olsun ---------
        new BsonDocument("$set", new BsonDocument("details",
            new BsonDocument("$ifNull", new BsonArray { new BsonDocument("$first", "$details"), new BsonDocument() })
        )),

        // --------- YAMA #2: ids alanları hep dizi olsun ----
        new BsonDocument("$set", new BsonDocument {
            { "details.GenreIds",    new BsonDocument("$ifNull", new BsonArray { "$details.GenreIds",    new BsonArray() }) },
            { "details.PlatformIds", new BsonDocument("$ifNull", new BsonArray { "$details.PlatformIds", new BsonArray() }) }
        })
    };

    if (officialOnly)
    {
        pipeline.Add(new BsonDocument("$match",
            new BsonDocument("$or", new BsonArray {
                new BsonDocument("details.Developer", new BsonDocument("$regex","Rockstar").Add("$options","i")),
                new BsonDocument("details.Publisher", new BsonDocument("$regex","Rockstar").Add("$options","i"))
            })));
    }

    pipeline.AddRange(new[]
    {
        // genres lookup  --------- YAMA #3: let.ids her zaman array ---------
        new BsonDocument("$lookup", new BsonDocument {
            { "from", "Genres" },
            { "let", new BsonDocument("ids",
                new BsonDocument("$ifNull", new BsonArray { "$details.GenreIds", new BsonArray() })) },
            { "pipeline", new BsonArray {
                new BsonDocument("$match", new BsonDocument("$expr",
                    new BsonDocument("$in", new BsonArray { "$_id", "$$ids" })))
            }},
            { "as", "genreDocs" }
        }),

        // platforms lookup  ------ YAMA #3: let.ids her zaman array ---------
        new BsonDocument("$lookup", new BsonDocument {
            { "from", "Platforms" },
            { "let", new BsonDocument("ids",
                new BsonDocument("$ifNull", new BsonArray { "$details.PlatformIds", new BsonArray() })) },
            { "pipeline", new BsonArray {
                new BsonDocument("$match", new BsonDocument("$expr",
                    new BsonDocument("$in", new BsonArray { "$_id", "$$ids" })))
            }},
            { "as", "platformDocs" }
        }),

        // sıralama ve limit
        new BsonDocument("$sort", new BsonDocument("Release_Date", -1)),
        new BsonDocument("$limit", limit),

        // son proje (temiz JSON)
        new BsonDocument("$project", new BsonDocument {
            { "_id", 0 },
            { "id", "$Id" },
            { "name", "$Game_Name" },
            { "releaseDate", "$Release_Date" },
            { "metacritic", "$Metacritic_Rating" },
            { "ggDbRating", "$GgDb_Rating" },
            { "mainImage", "$Main_image_URL" },
            { "popularity", "$Popularity" },
            { "developer", "$details.Developer" },
            { "publisher", "$details.Publisher" },
            { "ageRatings", "$details.Age_Ratings" },
            { "genres", new BsonDocument("$map", new BsonDocument {
                { "input", "$genreDocs" }, { "as", "g" }, { "in", "$$g.Name" }
            })},
            { "platforms", new BsonDocument("$map", new BsonDocument {
                { "input", "$platformDocs" }, { "as", "p" }, { "in", "$$p.Name" }
            })},
            { "about", "$details.Story" }
        })
    });

    var cursor = await _games.Aggregate<BsonDocument>(pipeline).ToListAsync();
    return cursor.Select(c => (object)MongoDB.Bson.Serialization.BsonSerializer.Deserialize<dynamic>(c)).ToList();
}


}