using MongoDB.Driver;
using MongoDB.Bson;
using CommentToGame.DTOs;
using CommentToGame.Models;
using CommentToGame.Data;

public sealed class GameEditService
{
    private readonly IMongoCollection<Game> _games;
    private readonly IMongoCollection<Game_Details> _details;
    private readonly IMongoCollection<Genre> _genres;
    private readonly IMongoCollection<Platform> _platforms;
    private readonly IMongoCollection<MinRequirement> _minReqs;
    private readonly IMongoCollection<RecRequirement> _recReqs;

    public GameEditService(MongoDbService db)
    {
        var database = db.Database!;
        _games      = database.GetCollection<Game>("Games");
        _details    = database.GetCollection<Game_Details>("GameDetails");
        _genres     = database.GetCollection<Genre>("Genres");
        _platforms  = database.GetCollection<Platform>("Platforms");
        _minReqs    = database.GetCollection<MinRequirement>("MinRequirements");
        _recReqs    = database.GetCollection<RecRequirement>("RecRequirements");
    }

    public async Task<bool> UpdateByNameAsync(string name, UpdateGameDto body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        // Game bul
        var gFilter = Builders<Game>.Filter.Regex(x => x.Game_Name, new BsonRegularExpression($"^{RegexEscape(name)}$", "i"));
        var game = await _games.Find(gFilter).FirstOrDefaultAsync(ct);
        if (game is null) return false;

        // ---------------- Game (üst doküman) ----------------
        var gUpdates = new List<UpdateDefinition<Game>>();
        var G = Builders<Game>.Update;

        if (body.ReleaseDate.HasValue) gUpdates.Add(G.Set(x => x.Release_Date, body.ReleaseDate));
        if (body.Metacritic.HasValue)  gUpdates.Add(G.Set(x => x.Metacritic_Rating, body.Metacritic));
        if (body.GgDbRating.HasValue)  gUpdates.Add(G.Set(x => x.GgDb_Rating, body.GgDbRating));
        if (body.Popularity.HasValue)  gUpdates.Add(G.Set(x => x.Popularity, body.Popularity));
        if (!string.IsNullOrWhiteSpace(body.MainImage)) gUpdates.Add(G.Set(x => x.Main_image_URL, body.MainImage));
        if (!string.IsNullOrWhiteSpace(body.Studio))    gUpdates.Add(G.Set(x => x.Studio, body.Studio));
        if (!string.IsNullOrWhiteSpace(body.Name))      gUpdates.Add(G.Set(x => x.Game_Name, body.Name!.Trim()));

        if (gUpdates.Count > 0)
            await _games.UpdateOneAsync(gFilter, Builders<Game>.Update.Combine(gUpdates), cancellationToken: ct);

        // Güncel id ve name’i al (rename olduysa)
        var current = await _games.Find(gFilter).FirstOrDefaultAsync(ct);
        var dFilter = Builders<Game_Details>.Filter.Eq(x => x.GameId, current.Id);

        // details upsert için ensure
        await _details.UpdateOneAsync(
            dFilter,
            Builders<Game_Details>.Update.SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                                         .SetOnInsert(x => x.GameId, current.Id),
            new UpdateOptions { IsUpsert = true }, ct);

        // ---------------- Details ----------------
        var D = Builders<Game_Details>.Update;
        var dUpdates = new List<UpdateDefinition<Game_Details>>();

        // düz alanlar
        if (!string.IsNullOrWhiteSpace(body.Developer)) dUpdates.Add(D.Set(x => x.Developer, body.Developer));
        if (!string.IsNullOrWhiteSpace(body.Publisher)) dUpdates.Add(D.Set(x => x.Publisher, body.Publisher));
        if (!string.IsNullOrWhiteSpace(body.About))     dUpdates.Add(D.Set(x => x.Story, body.About));

        // listeler (null => dokunma, empty => temizle)
        if (body.AgeRatings != null)      dUpdates.Add(D.Set(x => x.Age_Ratings, body.AgeRatings.Distinct().ToList()));
        if (body.Dlcs != null)            dUpdates.Add(D.Set(x => x.DLCs, body.Dlcs.Distinct().ToList()));
        if (body.Tags != null)            dUpdates.Add(D.Set(x => x.Tags, body.Tags.Distinct().ToList()));
        if (body.AudioLanguages != null)  dUpdates.Add(D.Set(x => x.Audio_Language, body.AudioLanguages.Distinct().ToList()));
        if (body.Subtitles != null)       dUpdates.Add(D.Set(x => x.Subtitles, body.Subtitles.Distinct().ToList()));
        if (body.InterfaceLanguages != null) dUpdates.Add(D.Set(x => x.Interface_Language, body.InterfaceLanguages.Distinct().ToList()));
        if (body.ContentWarnings != null) dUpdates.Add(D.Set(x => x.Content_Warnings, body.ContentWarnings.Distinct().ToList()));
        if (body.Engines != null)         dUpdates.Add(D.Set(x => x.Engines, body.Engines.Distinct().ToList()));
        if (body.Awards != null)          dUpdates.Add(D.Set(x => x.Awards, body.Awards.Distinct().ToList()));

        // Mağaza linkleri
        if (body.StoreLinks != null)
        {
            var mapped = body.StoreLinks.Select(s => new StoreLink
            {
                StoreId    = s.StoreId,
                Store      = s.Store,
                Slug       = s.Slug,
                Domain     = s.Domain,
                Url        = s.Url,
                ExternalId = s.ExternalId
            }).ToList();
            dUpdates.Add(D.Set(x => x.Store_Links, mapped));
        }

        // TimeToBeat (saat)
        if (body.TimeToBeatHastily.HasValue)   dUpdates.Add(D.Set(x => x.TimeToBeat_Hastily, body.TimeToBeatHastily));
        if (body.TimeToBeatNormally.HasValue)  dUpdates.Add(D.Set(x => x.TimeToBeat_Normally, body.TimeToBeatNormally));
        if (body.TimeToBeatCompletely.HasValue)dUpdates.Add(D.Set(x => x.TimeToBeat_Completely, body.TimeToBeatCompletely));

        // Genres/Platforms isimden ID'ye upsert
        if (body.Genres != null)
        {
            var ids = new List<string>();
            foreach (var name2 in body.Genres.Distinct())
            {
                var f = Builders<Genre>.Filter.Eq(x => x.Name, name2);
                var u = Builders<Genre>.Update.SetOnInsert(x => x.Name, name2)
                                              .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());
                var up = await _genres.FindOneAndUpdateAsync(f, u,
                    new FindOneAndUpdateOptions<Genre> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
                ids.Add(up.Id);
            }
            dUpdates.Add(D.Set(x => x.GenreIds, ids));
        }

        if (body.Platforms != null)
        {
            var ids = new List<string>();
            foreach (var name2 in body.Platforms.Distinct())
            {
                var f = Builders<Platform>.Filter.Eq(x => x.Name, name2);
                var u = Builders<Platform>.Update.SetOnInsert(x => x.Name, name2)
                                                 .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString());
                var up = await _platforms.FindOneAndUpdateAsync(f, u,
                    new FindOneAndUpdateOptions<Platform> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
                ids.Add(up.Id);
            }
            dUpdates.Add(D.Set(x => x.PlatformIds, ids));
        }

        // Min/Rec Requirements metinleri -> ilgili koleksiyonlara upsert edip id set et
        if (body.MinRequirement != null)
        {
            var id = await UpsertMinRequirementAsync(body.MinRequirement, ct);
            dUpdates.Add(id is null ? D.Unset(x => x.MinRequirementId) : D.Set(x => x.MinRequirementId, id));
        }
        if (body.RecRequirement != null)
        {
            var id = await UpsertRecRequirementAsync(body.RecRequirement, ct);
            dUpdates.Add(id is null ? D.Unset(x => x.RecRequirementId) : D.Set(x => x.RecRequirementId, id));
        }

        if (dUpdates.Count > 0)
            await _details.UpdateOneAsync(dFilter, Builders<Game_Details>.Update.Combine(dUpdates), cancellationToken: ct);

        return true;
    }

    // ----------------- GET tarafı (formu doldurmak için) -----------------
    public async Task<UpdateGameDto?> GetEditableByNameAsync(string name, CancellationToken ct = default)
    {
        var g = await _games.Find(Builders<Game>.Filter.Regex(x => x.Game_Name, new BsonRegularExpression($"^{RegexEscape(name)}$", "i"))).FirstOrDefaultAsync(ct);
        if (g is null) return null;

        var d = await _details.Find(x => x.GameId == g.Id).FirstOrDefaultAsync(ct) ?? new Game_Details { GameId = g.Id };

        // ids -> isim
        var genreNames = d.GenreIds?.Count > 0
            ? (await _genres.Find(Builders<Genre>.Filter.In(x => x.Id, d.GenreIds)).ToListAsync(ct)).Select(x => x.Name).ToList()
            : new List<string>();
        var platformNames = d.PlatformIds?.Count > 0
            ? (await _platforms.Find(Builders<Platform>.Filter.In(x => x.Id, d.PlatformIds)).ToListAsync(ct)).Select(x => x.Name).ToList()
            : new List<string>();

        // min/rec metinlerini getir
        string? minText = null, recText = null;
        if (!string.IsNullOrEmpty(d.MinRequirementId))
            minText = (await _minReqs.Find(x => x.Id == d.MinRequirementId).FirstOrDefaultAsync(ct))?.Text;
        if (!string.IsNullOrEmpty(d.RecRequirementId))
            recText = (await _recReqs.Find(x => x.Id == d.RecRequirementId).FirstOrDefaultAsync(ct))?.Text;

        return new UpdateGameDto
        {
            Name = g.Game_Name,
            ReleaseDate = g.Release_Date,
            Metacritic = g.Metacritic_Rating,
            GgDbRating = g.GgDb_Rating,
            MainImage  = g.Main_image_URL,
            Popularity = g.Popularity,
            Studio     = g.Studio,

            Developer = d.Developer,
            Publisher = d.Publisher,
            About     = d.Story,

            AgeRatings = d.Age_Ratings ?? new(),
            Dlcs       = d.DLCs ?? new(),
            Tags       = d.Tags ?? new(),
            Genres     = genreNames,
            Platforms  = platformNames,

            MinRequirement = minText,
            RecRequirement = recText,

            AudioLanguages    = d.Audio_Language ?? new(),
            Subtitles         = d.Subtitles ?? new(),
            InterfaceLanguages= d.Interface_Language ?? new(),
            ContentWarnings   = d.Content_Warnings ?? new(),

            StoreLinks = (d.Store_Links ?? new()).Select(s => new StoreLinkDto {
                StoreId = s.StoreId, Store = s.Store, Slug = s.Slug, Domain = s.Domain, Url = s.Url, ExternalId = s.ExternalId
            }).ToList(),

            TimeToBeatHastily   = d.TimeToBeat_Hastily,
            TimeToBeatNormally  = d.TimeToBeat_Normally,
            TimeToBeatCompletely= d.TimeToBeat_Completely,

            Engines = d.Engines ?? new(),
            Awards  = d.Awards  ?? new()
        };
    }

    public async Task<UpdateGameDto?> GetEditableByNameLikeAsync(string name, int limit = 1, CancellationToken ct = default)
    {
        var filter = Builders<Game>.Filter.Regex(x => x.Game_Name, new BsonRegularExpression(RegexEscape(name), "i"));
        var g = await _games.Find(filter).SortByDescending(x => x.Popularity).Limit(limit).FirstOrDefaultAsync(ct);
        return g is null ? null : await GetEditableByNameAsync(g.Game_Name, ct);
    }

    // ----------------- helpers -----------------
    private static string RegexEscape(string s) =>
        string.IsNullOrEmpty(s) ? s : System.Text.RegularExpressions.Regex.Escape(s);

    private async Task<string?> UpsertMinRequirementAsync(string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var f = Builders<MinRequirement>.Filter.Eq(x => x.Text, text.Trim());
        var u = Builders<MinRequirement>.Update
            .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
            .SetOnInsert(x => x.Text, text.Trim());
        var doc = await _minReqs.FindOneAndUpdateAsync(f, u,
            new FindOneAndUpdateOptions<MinRequirement> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
        return doc?.Id;
    }

    private async Task<string?> UpsertRecRequirementAsync(string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var f = Builders<RecRequirement>.Filter.Eq(x => x.Text, text.Trim());
        var u = Builders<RecRequirement>.Update
            .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
            .SetOnInsert(x => x.Text, text.Trim());
        var doc = await _recReqs.FindOneAndUpdateAsync(f, u,
            new FindOneAndUpdateOptions<RecRequirement> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
        return doc?.Id;
    }
}
