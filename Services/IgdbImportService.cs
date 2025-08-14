using CommentToGame.Data;
using CommentToGame.Models;
using CommentToGame.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using static CommentToGame.DTOs.IGdbDto;

public class IgdbImportService
{
        private readonly IIgdbClient _igdb;
        private readonly IMongoCollection<Game> _games;
        private readonly IMongoCollection<Game_Details> _details;
        private readonly IMongoCollection<Genre> _genres;
        private readonly IMongoCollection<Platform> _platforms;
        private readonly IMongoCollection<MinRequirement> _minReqs;     // IGDB sistem gereksinimi sağlamaz – boş kalabilir
        private readonly IMongoCollection<RecRequirement> _recReqs;     // IGDB sistem gereksinimi sağlamaz – boş kalabilir

        public IgdbImportService(IIgdbClient igdb, MongoDbService svc)
        {
            _igdb = igdb;
            var db = svc.Database!;
            _games      = db.GetCollection<Game>("Games");
            _details    = db.GetCollection<Game_Details>("GameDetails");
            _genres     = db.GetCollection<Genre>("Genres");
            _platforms  = db.GetCollection<Platform>("Platforms");
            _minReqs    = db.GetCollection<MinRequirement>("MinRequirements");
            _recReqs    = db.GetCollection<RecRequirement>("RecRequirements");
        }

        // RAWG: ImportAsync muadili – IGDB sayfalı listeyi çekip kaydeder
        public async Task<int> ImportAsync(int pages = 1, int pageSize = 40, CancellationToken ct = default)
        {
            int count = 0;
            for (int page = 1; page <= pages; page++)
            {
                var list = await _igdb.GetGamesAsync(page, pageSize, ct);
                foreach (var g in list.Results)
                {
                    var detail = await _igdb.GetGameDetailAsync(g.Id, ct);
                    if (detail is null) continue;
                    await UpsertOneAsync(detail, ct);
                    count++;
                }
            }
            return count;
        }

        // RAWG: TestFetchAsync muadili
        public async Task<int> TestFetchAsync(CancellationToken ct = default)
        {
            var page = await _igdb.GetGamesAsync(page: 1, pageSize: 3, ct);
            return page.Results?.Count ?? 0;
        }

        // RAWG: ImportAllBySearchAsync muadili (aynı imza ve akış)
        public async Task<int> ImportAllBySearchAsync(string query, int pageSize = 40, int maxPages = 5, int delayMs = 250, CancellationToken ct = default)
        {
            int imported = 0;
            int page = 1;
            string? next;
            do
            {
                var list = await _igdb.SearchGamesAsync(query, page, pageSize, ct);
                foreach (var s in list.Results)
                {
                    var detail = await _igdb.GetGameDetailAsync(s.Id, ct);
                    if (detail == null) continue;
                    await UpsertOneAsync(detail, ct);
                    imported++;
                }
                next = list.Next;
                page++;
                if (delayMs > 0) await Task.Delay(delayMs, ct);
            }
            while (!string.IsNullOrEmpty(next) && page <= maxPages);

            return imported;
        }

        // ============== Asıl upsert – RAWG UpsertOneAsync ile birebir ==============
        public async Task UpsertOneAsync(IgdbGameDetail detail, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(detail.Name)) return;

            // 1) Genres upsert
            var genreIds = new List<string>();
            foreach (var name in detail.Genres.Distinct())
            {
                var gFilter = Builders<Genre>.Filter.Eq(x => x.Name, name);
                var gUpdate = Builders<Genre>.Update
                    .SetOnInsert(x => x.Name, name)
                    .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());
                var gUp = await _genres.FindOneAndUpdateAsync(
                    gFilter, gUpdate,
                    new FindOneAndUpdateOptions<Genre> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
                genreIds.Add(gUp.Id);
            }

            // 2) Platforms upsert
            var platformIds = new List<string>();
            foreach (var name in detail.Platforms.Distinct())
            {
                var pFilter = Builders<Platform>.Filter.Eq(x => x.Name, name);
                var pUpdate = Builders<Platform>.Update
                    .SetOnInsert(x => x.Name, name)
                    .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());
                var pUp = await _platforms.FindOneAndUpdateAsync(
                    pFilter, pUpdate,
                    new FindOneAndUpdateOptions<Platform> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
                platformIds.Add(pUp.Id);
            }

            // 3) Game upsert (isim bazlı)
            var gameFilter = Builders<Game>.Filter.Eq(x => x.Game_Name, detail.Name);
            
            var gameUpdate = Builders<Game>.Update
                .Set(x => x.Release_Date, detail.ReleaseDate)
                .Set(x => x.Metacritic_Rating, detail.Metacritic)
                .Set(x => x.GgDb_Rating, detail.Rating.HasValue ? (int?)Math.Round(detail.Rating.Value) : null) // IGDB /100
                .Set(x => x.Popularity, detail.Added)
                .Set(x => x.Main_image_URL, detail.BackgroundImage)
                .Set(x => x.CompanyIds, new List<string>())
                .SetOnInsert(x => x.Game_Name, detail.Name)
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());

            var gameFromDb = await _games.FindOneAndUpdateAsync(
                gameFilter, gameUpdate,
                new FindOneAndUpdateOptions<Game> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);

            // 4) Game_Details upsert
            var detFilter = Builders<Game_Details>.Filter.Eq(x => x.GameId, gameFromDb.Id);

            var developer = detail.Developers.FirstOrDefault();
            var publisher = detail.Publishers.FirstOrDefault();
            var ageRatingNames = detail.AgeRatings.Distinct().ToList();
            var tagNames = detail.Tags.Distinct().ToList();
            var ttb = await _igdb.GetTimeToBeatAsync(detail.Id, ct);
            var awards = await _igdb.GetAwardsLikeEventsAsync(detail.Id, ct);
            
            
            var detUpdate = Builders<Game_Details>.Update
                .Set(x => x.Developer, developer)
                .Set(x => x.Publisher, publisher)
                .Set(x => x.GenreIds, genreIds)
                .Set(x => x.PlatformIds, platformIds)
                .Set(x => x.Story, detail.Summary)
                .Set(x => x.Age_Ratings, ageRatingNames)
                .Set(x => x.Tags, tagNames)
                .Set(x => x.Engines, detail.Engines)
                .Set(x => x.Audio_Language, detail.AudioLanguages ?? new List<string>())
    .            Set(x => x.Subtitles, detail.Subtitles ?? new List<string>())
                .Set(x => x.Interface_Language, detail.InterfaceLanguages ?? new List<string>())
                .Set(x => x.Content_Warnings, detail.ContentWarnings ?? new List<string>())
                .SetOnInsert(x => x.GameId, gameFromDb.Id)
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());

        if (awards is { Count: > 0 })
    detUpdate = detUpdate.Set(x => x.Awards, awards);

        if (ttb != null)
        {
            int? ToMinutes(int? s) => s.HasValue ? s.Value / 3600 : (int?)null;

            var h = ToMinutes(ttb.Hastily);
            var n = ToMinutes(ttb.Normally);
            var c = ToMinutes(ttb.Completely);

            if (h.HasValue) detUpdate = detUpdate.Set(x => x.TimeToBeat_Hastily, h);
            if (n.HasValue) detUpdate = detUpdate.Set(x => x.TimeToBeat_Normally, n);
            if (c.HasValue) detUpdate = detUpdate.Set(x => x.TimeToBeat_Completely, c);
        }

        var storeLinks = await _igdb.GetStoreLinksAsync(detail.Id, ct);
        if (storeLinks?.Count > 0)
        {
            detUpdate = detUpdate.Set(x => x.Store_Links, storeLinks);
        }

            // IGDB sistem gereksinimi sağlamadığı için Min/Rec null bırakılır.
        await _details.UpdateOneAsync(detFilter, detUpdate, new UpdateOptions { IsUpsert = true }, ct);

            // (opsiyonel) DLC/Additions (varsa)
            try
            {
                var additions = await _igdb.GetGameAdditionsAsync(detail.Id, ct);
                var dlcNames = additions?.Results?
                    .Select(a => a.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList() ?? new List<string>();

                var dlcUpdate = Builders<Game_Details>.Update.Set(x => x.DLCs, dlcNames);
                await _details.UpdateOneAsync(detFilter, dlcUpdate, new UpdateOptions { IsUpsert = true }, ct);
            }
            catch
            {
                // yoksay
            }
        }

        // ============= (İsteğe bağlı) DB sorgu yardımcıları =============
        public async Task<List<Game>> GetAllGamesAsync(int skip, int take)
        {
            if (skip < 0) skip = 0; if (take <= 0) take = 50;
            return await _games.Find(FilterDefinition<Game>.Empty).Skip(skip).Limit(take).ToListAsync();
        }

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

            // details hep obje (yoksa boş obje)
            new BsonDocument("$set", new BsonDocument("details",
                new BsonDocument("$ifNull", new BsonArray { new BsonDocument("$first", "$details"), new BsonDocument() })
            )),

            // ids alanları hep dizi
            new BsonDocument("$set", new BsonDocument {
                { "details.GenreIds",    new BsonDocument("$ifNull", new BsonArray { "$details.GenreIds",    new BsonArray() }) },
                { "details.PlatformIds", new BsonDocument("$ifNull", new BsonArray { "$details.PlatformIds", new BsonArray() }) },
                { "details.Age_Ratings", new BsonDocument("$ifNull", new BsonArray { "$details.Age_Ratings", new BsonArray() }) },
                { "details.DLCs",        new BsonDocument("$ifNull", new BsonArray { "$details.DLCs",        new BsonArray() }) },
                { "details.Tags",        new BsonDocument("$ifNull", new BsonArray { "$details.Tags",        new BsonArray() }) }
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
            // genres lookup
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

            // platforms lookup
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

            // Min/Rec requirements LOOKUP (IGDB vermiyor ama şema için null-safe)
            new BsonDocument("$lookup", new BsonDocument {
                { "from", "MinRequirements" },
                { "localField", "details.MinRequirementId" },
                { "foreignField", "_id" },
                { "as", "minReqDoc" }
            }),
            new BsonDocument("$lookup", new BsonDocument {
                { "from", "RecRequirements" },
                { "localField", "details.RecRequirementId" },
                { "foreignField", "_id" },
                { "as", "recReqDoc" }
            }),
            new BsonDocument("$set", new BsonDocument {
                { "minRequirement", new BsonDocument("$ifNull",
                    new BsonArray { new BsonDocument("$first", "$minReqDoc.Text"), BsonNull.Value }) },
                { "recRequirement", new BsonDocument("$ifNull",
                    new BsonArray { new BsonDocument("$first", "$recReqDoc.Text"), BsonNull.Value }) }
            }),

            new BsonDocument("$sort", new BsonDocument("Release_Date", -1)),
            new BsonDocument("$limit", limit),

            new BsonDocument("$set", new BsonDocument {
    { "details.GenreIds",    new BsonDocument("$ifNull", new BsonArray { "$details.GenreIds",    new BsonArray() }) },
    { "details.PlatformIds", new BsonDocument("$ifNull", new BsonArray { "$details.PlatformIds", new BsonArray() }) },
    { "details.Awards",      new BsonDocument("$ifNull", new BsonArray { "$details.Awards",      new BsonArray() }) } // NEW
    }),

            // === RAWG ile birebir projection ===
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
                { "minRequirement", "$minRequirement" },   // <-- daima var (null olabilir)
                { "recRequirement", "$recRequirement" },   // <-- daima var (null olabilir)
                { "ageRatings", "$details.Age_Ratings" },
                { "dlcs", "$details.DLCs" },
                { "audioLanguages", "$details.Audio_Language" },
                { "subtitles", "$details.Subtitles" },
                { "interfaceLanguages", "$details.Interface_Language" },
                { "contentWarnings", "$details.Content_Warnings" },
                { "storeLinks", "$details.Store_Links" },
                { "timeToBeat", new BsonDocument {
                { "hastily",    "$details.TimeToBeat_Hastily" },
                { "normally",   "$details.TimeToBeat_Normally" },
                { "completely", "$details.TimeToBeat_Completely" }
                } },
                { "engines", "$details.Engines" },
                { "awards", "$details.Awards" },
                { "tags", "$details.Tags" },
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


        // IGDB sistem gereksinimi sağlamaz; yine de imzaları koruyoruz.
        private Task<string?> UpsertMinRequirementAsync(string? text) => Task.FromResult<string?>(null);
        private Task<string?> UpsertRecRequirementAsync(string? text) => Task.FromResult<string?>(null);
}

