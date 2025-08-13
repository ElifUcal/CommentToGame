namespace CommentToGame.Services
{
    using System.Text.RegularExpressions;
    using CommentToGame.Data;
    using CommentToGame.DTOs;
    using CommentToGame.Models;
    using MongoDB.Bson;
    using MongoDB.Driver;

    public sealed class GameEditService
    {
        private readonly IMongoCollection<Game> _games;
        private readonly IMongoCollection<Game_Details> _details;
        private readonly IMongoCollection<Genre> _genres;
        private readonly IMongoCollection<Platform> _platforms;
        private readonly IMongoCollection<MinRequirement> _minReqs;
        private readonly IMongoCollection<RecRequirement> _recReqs;

        public GameEditService(MongoDbService svc)
        {
            var db = svc.Database!;
            _games = db.GetCollection<Game>("Games");
            _details = db.GetCollection<Game_Details>("GameDetails");
            _genres = db.GetCollection<Genre>("Genres");
            _platforms = db.GetCollection<Platform>("Platforms");
            _minReqs = db.GetCollection<MinRequirement>("MinRequirements");
            _recReqs = db.GetCollection<RecRequirement>("RecRequirements");
        }

        private static string? Norm(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static List<string> CleanList(IEnumerable<string>? src)
            => src?.Where(x => !string.IsNullOrWhiteSpace(x))
                   .Select(x => x.Trim())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList() ?? new List<string>();

        // ---------- READ (for form) ----------
        public async Task<UpdateGameDto?> GetEditableByNameAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var pattern = "^" + Regex.Escape(name.Trim()) + "$"; // tam eşleşme
            var filter = Builders<Game>.Filter.Regex(g => g.Game_Name, new BsonRegularExpression(pattern, "i"));
            var game = await _games.Find(filter).FirstOrDefaultAsync(ct);
            if (game is null) return null;

            var det = await _details.Find(x => x.GameId == game.Id).FirstOrDefaultAsync(ct)
                      ?? new Game_Details { GameId = game.Id };

            var genreNames = (det.GenreIds != null && det.GenreIds.Count > 0)
                ? await _genres.Find(x => det.GenreIds.Contains(x.Id)).Project(x => x.Name).ToListAsync(ct)
                : new List<string>();
            var platformNames = (det.PlatformIds != null && det.PlatformIds.Count > 0)
                ? await _platforms.Find(x => det.PlatformIds.Contains(x.Id)).Project(x => x.Name).ToListAsync(ct)
                : new List<string>();

            string? minReq = null, recReq = null;
            if (!string.IsNullOrEmpty(det.MinRequirementId))
                minReq = (await _minReqs.Find(x => x.Id == det.MinRequirementId).FirstOrDefaultAsync(ct))?.Text;
            if (!string.IsNullOrEmpty(det.RecRequirementId))
                recReq = (await _recReqs.Find(x => x.Id == det.RecRequirementId).FirstOrDefaultAsync(ct))?.Text;

            return new UpdateGameDto
            {
                Name = game.Game_Name,
                ReleaseDate = game.Release_Date,
                Metacritic = game.Metacritic_Rating,
                GgDbRating = game.GgDb_Rating,
                MainImage = game.Main_image_URL,
                Popularity = game.Popularity,
                Developer = det.Developer,
                Publisher = det.Publisher,
                About = det.Story,
                AgeRatings = det.Age_Ratings ?? new List<string>(),
                Dlcs = det.DLCs ?? new List<string>(),
                Tags = det.Tags ?? new List<string>(),
                Genres = genreNames,
                Platforms = platformNames,
                MinRequirement = minReq,
                RecRequirement = recReq
            };
        }

        public async Task<UpdateGameDto?> GetEditableByNameLikeAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var filter = Builders<Game>.Filter.Regex(g => g.Game_Name, new BsonRegularExpression(Regex.Escape(name.Trim()), "i"));
            var game = await _games.Find(filter)
                                   .Sort(Builders<Game>.Sort.Descending(x => x.Popularity).Descending(x => x.Release_Date))
                                   .FirstOrDefaultAsync(ct);
            if (game is null) return null;
            return await GetEditableByNameAsync(game.Game_Name!, ct);
        }

        // ---------- WRITE (merge update by name) ----------
        public async Task<bool> UpdateByNameAsync(string name, DTOs.UpdateGameDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var pattern = "^" + Regex.Escape(name.Trim()) + "$"; // tam eşleşme
            var gameFilter = Builders<Game>.Filter.Regex(g => g.Game_Name, new BsonRegularExpression(pattern, "i"));
            var game = await _games.Find(gameFilter).FirstOrDefaultAsync(ct);
            if (game is null) return false;

            // ---- Game fields
            var gameUpdates = new List<UpdateDefinition<Game>>();
            if (dto.Name != null)        gameUpdates.Add(Builders<Game>.Update.Set(x => x.Game_Name, Norm(dto.Name)));
            if (dto.ReleaseDate != null) gameUpdates.Add(Builders<Game>.Update.Set(x => x.Release_Date, dto.ReleaseDate));
            if (dto.Metacritic != null)  gameUpdates.Add(Builders<Game>.Update.Set(x => x.Metacritic_Rating, dto.Metacritic));
            if (dto.GgDbRating != null)  gameUpdates.Add(Builders<Game>.Update.Set(x => x.GgDb_Rating, dto.GgDbRating));
            if (dto.MainImage != null)   gameUpdates.Add(Builders<Game>.Update.Set(x => x.Main_image_URL, Norm(dto.MainImage)));
            if (dto.Popularity != null)  gameUpdates.Add(Builders<Game>.Update.Set(x => x.Popularity, dto.Popularity));

            if (gameUpdates.Count > 0)
                await _games.UpdateOneAsync(gameFilter, Builders<Game>.Update.Combine(gameUpdates), cancellationToken: ct);

            // ---- Details fields
            var detFilter = Builders<Game_Details>.Filter.Eq(x => x.GameId, game.Id);
            var detUpd = new List<UpdateDefinition<Game_Details>>
            {
                Builders<Game_Details>.Update.SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString()),
                Builders<Game_Details>.Update.SetOnInsert(x => x.GameId, game.Id)
            };

            if (dto.Developer != null) detUpd.Add(Builders<Game_Details>.Update.Set(x => x.Developer, Norm(dto.Developer)));
            if (dto.Publisher != null) detUpd.Add(Builders<Game_Details>.Update.Set(x => x.Publisher, Norm(dto.Publisher)));
            if (dto.About != null)     detUpd.Add(Builders<Game_Details>.Update.Set(x => x.Story, dto.About));

            if (dto.AgeRatings != null) detUpd.Add(Builders<Game_Details>.Update.Set(x => x.Age_Ratings, CleanList(dto.AgeRatings)));
            if (dto.Dlcs != null)       detUpd.Add(Builders<Game_Details>.Update.Set(x => x.DLCs, CleanList(dto.Dlcs)));
            if (dto.Tags != null)       detUpd.Add(Builders<Game_Details>.Update.Set(x => x.Tags, CleanList(dto.Tags)));

            if (dto.Genres != null)
            {
                var ids = new List<string>();
                foreach (var nm in CleanList(dto.Genres))
                {
                    var f = Builders<Genre>.Filter.Eq(x => x.Name, nm);
                    var u = Builders<Genre>.Update
                        .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                        .SetOnInsert(x => x.Name, nm);
                    var up = await _genres.FindOneAndUpdateAsync(f, u,
                        new FindOneAndUpdateOptions<Genre> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
                    ids.Add(up.Id);
                }
                detUpd.Add(Builders<Game_Details>.Update.Set(x => x.GenreIds, ids));
            }

            if (dto.Platforms != null)
            {
                var ids = new List<string>();
                foreach (var nm in CleanList(dto.Platforms))
                {
                    var f = Builders<Platform>.Filter.Eq(x => x.Name, nm);
                    var u = Builders<Platform>.Update
                        .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                        .SetOnInsert(x => x.Name, nm);
                    var up = await _platforms.FindOneAndUpdateAsync(f, u,
                        new FindOneAndUpdateOptions<Platform> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
                    ids.Add(up.Id);
                }
                detUpd.Add(Builders<Game_Details>.Update.Set(x => x.PlatformIds, ids));
            }

            // Min/Rec requirement: null = dokunma, empty = sıfırla, text = upsert & set
            if (dto.MinRequirement != null)
            {
                var norm = Norm(dto.MinRequirement);
                if (norm == null)
                    detUpd.Add(Builders<Game_Details>.Update.Set(x => x.MinRequirementId, null));
                else
                {
                    var minId = await UpsertMinRequirementAsync(norm, ct);
                    detUpd.Add(Builders<Game_Details>.Update.Set(x => x.MinRequirementId, minId));
                }
            }
            if (dto.RecRequirement != null)
            {
                var norm = Norm(dto.RecRequirement);
                if (norm == null)
                    detUpd.Add(Builders<Game_Details>.Update.Set(x => x.RecRequirementId, null));
                else
                {
                    var recId = await UpsertRecRequirementAsync(norm, ct);
                    detUpd.Add(Builders<Game_Details>.Update.Set(x => x.RecRequirementId, recId));
                }
            }

            if (detUpd.Count > 0)
                await _details.UpdateOneAsync(detFilter, Builders<Game_Details>.Update.Combine(detUpd), new UpdateOptions { IsUpsert = true }, ct);

            return true;
        }

        private async Task<string?> UpsertMinRequirementAsync(string text, CancellationToken ct)
        {
            var filter = Builders<MinRequirement>.Filter.Eq(x => x.Text, text);
            var update = Builders<MinRequirement>.Update
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                .SetOnInsert(x => x.Text, text);
            var doc = await _minReqs.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<MinRequirement> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
            return doc?.Id;
        }

        private async Task<string?> UpsertRecRequirementAsync(string text, CancellationToken ct)
        {
            var filter = Builders<RecRequirement>.Filter.Eq(x => x.Text, text);
            var update = Builders<RecRequirement>.Update
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                .SetOnInsert(x => x.Text, text);
            var doc = await _recReqs.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<RecRequirement> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
            return doc?.Id;
        }
    }
}