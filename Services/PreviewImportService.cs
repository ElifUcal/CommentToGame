using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.Data;            // MongoDbService
using CommentToGame.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CommentToGame.Services;

public class PreviewImportService
{
    private readonly IMongoCollection<Game> _games;
    private readonly IMongoCollection<Game_Details> _details;
    private readonly IMongoCollection<Genre> _genres;
    private readonly IMongoCollection<Platform> _platforms;
    private readonly IMongoCollection<MinRequirement> _minReqs;
    private readonly IMongoCollection<RecRequirement> _recReqs;

    private readonly IMongoCollection<Gallery>? _galleries; // opsiyonel

    public PreviewImportService(MongoDbService svc)
    {
        var db = svc.Database ?? throw new InvalidOperationException("Mongo database is null.");
        _games     = db.GetCollection<Game>("Games");
        _details   = db.GetCollection<Game_Details>("GameDetails");
        _genres    = db.GetCollection<Genre>("Genres");
        _platforms = db.GetCollection<Platform>("Platforms");
        _minReqs   = db.GetCollection<MinRequirement>("MinRequirements");
        _recReqs   = db.GetCollection<RecRequirement>("RecRequirements");

        try { _galleries = db.GetCollection<Gallery>("Galleries"); } catch { /* optional */ }
    }

    public async Task<List<string>> UpsertManyAsync(IEnumerable<GameMerge.MergedGameDto> list, CancellationToken ct = default)
    {
        var ids = new List<string>();
        foreach (var dto in list)
        {
            ct.ThrowIfCancellationRequested();
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name)) continue;

            var id = await UpsertOneAsync(dto, ct);
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }
        return ids;
    }

    public async Task<string> UpsertOneAsync(GameMerge.MergedGameDto dto, CancellationToken ct = default)
    {
        // ---------- 1) Genres upsert ----------
        var genreIds = new List<string>();
        if (dto.Genres != null)
        {
            foreach (var name in dto.Genres.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                var filter = Builders<Genre>.Filter.Eq(x => x.Name, name);
                var update = Builders<Genre>.Update
                    .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                    .SetOnInsert(x => x.Name, name);

                var up = await _genres.FindOneAndUpdateAsync(
                    filter, update,
                    new FindOneAndUpdateOptions<Genre> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);

                genreIds.Add(up.Id);
            }
        }

        // ---------- 2) Platforms upsert ----------
        var platformIds = new List<string>();
        if (dto.Platforms != null)
        {
            foreach (var name in dto.Platforms.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                var filter = Builders<Platform>.Filter.Eq(x => x.Name, name);
                var update = Builders<Platform>.Update
                    .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                    .SetOnInsert(x => x.Name, name);

                var up = await _platforms.FindOneAndUpdateAsync(
                    filter, update,
                    new FindOneAndUpdateOptions<Platform> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);

                platformIds.Add(up.Id);
            }
        }

        // ---------- 3) Game upsert (isim bazlı) ----------
        var gameFilter = Builders<Game>.Filter.Eq(x => x.Game_Name, dto.Name!.Trim());
        var gameUpdate = Builders<Game>.Update
            .Set(x => x.Release_Date, dto.ReleaseDate)
            .Set(x => x.Metacritic_Rating, dto.Metacritic)
            .Set(x => x.GgDb_Rating, dto.GgDbRating)
            .Set(x => x.Popularity, dto.Popularity)
            .Set(x => x.Main_image_URL, dto.MainImage)
            .Set(x => x.CompanyIds, new List<string>())
                        
            .SetOnInsert(x => x.Game_Name, dto.Name!.Trim())
            .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
            .SetOnInsert(x => x.Createdat, DateTime.UtcNow);        // varsa alan

        if (dto.Cast is { Count: > 0 }) gameUpdate = gameUpdate.Set(x => x.Cast, dto.Cast.Distinct().ToList());
        if (dto.Crew is { Count: > 0 }) gameUpdate = gameUpdate.Set(x => x.Crew, dto.Crew.Distinct().ToList());

        var game = await _games.FindOneAndUpdateAsync(
            gameFilter, gameUpdate,
            new FindOneAndUpdateOptions<Game> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);

        // ---------- 4) Game_Details upsert (Combine + koşullu) ----------
        var detFilter = Builders<Game_Details>.Filter.Eq(x => x.GameId, game.Id);
        var ub = Builders<Game_Details>.Update;
        var updates = new List<UpdateDefinition<Game_Details>>
        {
            ub.Set(x => x.Developer, dto.Developer),
            ub.Set(x => x.Publisher, dto.Publisher),
            ub.Set(x => x.GenreIds, genreIds),
            ub.Set(x => x.PlatformIds, platformIds),
            ub.Set(x => x.Story, dto.About),
            ub.Set(x => x.Age_Ratings, dto.AgeRatings ?? new List<string>()),
            ub.Set(x => x.Tags, dto.Tags ?? new List<string>()),
            ub.Set(x => x.Engines, dto.Engines ?? new List<string>()),
            ub.Set(x => x.Audio_Language, dto.AudioLanguages ?? new List<string>()),
            ub.Set(x => x.Subtitles, dto.Subtitles ?? new List<string>()),
            ub.Set(x => x.Interface_Language, dto.InterfaceLanguages ?? new List<string>()),
            ub.Set(x => x.Content_Warnings, dto.ContentWarnings ?? new List<string>()),
            ub.Set(x => x.DLCs, dto.Dlcs ?? new List<string>()),
            ub.SetOnInsert(x => x.GameId, game.Id),
            ub.SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString()),
            
        };

        if (dto.Awards is { Count: > 0 })
            updates.Add(ub.Set(x => x.Awards, dto.Awards.Distinct().ToList()));

        if (dto.TimeToBeat != null)
        {
            if (dto.TimeToBeat.Hastily.HasValue)
                updates.Add(ub.Set(x => x.TimeToBeat_Hastily, dto.TimeToBeat.Hastily));
            if (dto.TimeToBeat.Normally.HasValue)
                updates.Add(ub.Set(x => x.TimeToBeat_Normally, dto.TimeToBeat.Normally));
            if (dto.TimeToBeat.Completely.HasValue)
                updates.Add(ub.Set(x => x.TimeToBeat_Completely, dto.TimeToBeat.Completely));
        }

        // ---------- 4.a) REQUIREMENTS referans upsert ----------
        var minRef = await UpsertMinRequirementAsync(dto.MinRequirement, ct);
        var recRef = await UpsertRecRequirementAsync(dto.RecRequirement, ct);

        if (minRef is not null)
        {
            updates.Add(ub.Set(x => x.MinRequirementId, minRef.Value.id));
            // denormalize
        }

        if (recRef is not null)
        {
            updates.Add(ub.Set(x => x.RecRequirementId, recRef.Value.id));
            
        }

        // Eski gömülü obje alanlarını temizle (varsa)
        updates.Add(ub.Unset("MinRequirement"));
        updates.Add(ub.Unset("RecRequirement"));

        if (dto.StoreLinks is { Count: > 0 })
            updates.Add(ub.Set(x => x.Store_Links, dto.StoreLinks));

        await _details.UpdateOneAsync(detFilter, ub.Combine(updates), new UpdateOptions { IsUpsert = true }, ct);

        // ---------- 5) Gallery ----------
        if (_galleries != null && dto.Images is { Count: > 0 })
        {
            await _galleries.DeleteManyAsync(x => x.GameId == game.Id, ct);

            var batch = dto.Images
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => new Gallery
                {
                    Id     = ObjectId.GenerateNewId().ToString(),
                    GameId = game.Id,
                    Urls = null
                })
                .ToList();

            if (batch.Count > 0)
                await _galleries.InsertManyAsync(batch, cancellationToken: ct);
        }

        return game.Id;
    }

    // ===== Helpers: Requirements upsert (ID öncelikli, yoksa text dedupe) =====

    private async Task<(string id, string text)?> UpsertMinRequirementAsync(GameMerge.RequirementInput? input, CancellationToken ct)
    {
        if (input is null) return null;

        if (!string.IsNullOrWhiteSpace(input.Id))
        {
            var existing = await _minReqs.Find(x => x.Id == input.Id).FirstOrDefaultAsync(ct);
            if (existing != null) return (existing.Id, existing.Text);

            if (!string.IsNullOrWhiteSpace(input.Text))
            {
                var doc = new MinRequirement { Id = input.Id, Text = input.Text.Trim() };
                await _minReqs.InsertOneAsync(doc, cancellationToken: ct);
                return (doc.Id, doc.Text);
            }
            return null;
        }

        var text = input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        // aynı text varsa onu kullan
        var found = await _minReqs.Find(x => x.Text == text).FirstOrDefaultAsync(ct);
        if (found != null) return (found.Id, found.Text);

        var ins = new MinRequirement { Id = ObjectId.GenerateNewId().ToString(), Text = text };
        await _minReqs.InsertOneAsync(ins, cancellationToken: ct);
        return (ins.Id, ins.Text);
    }

    private async Task<(string id, string text)?> UpsertRecRequirementAsync(GameMerge.RequirementInput? input, CancellationToken ct)
    {
        if (input is null) return null;

        if (!string.IsNullOrWhiteSpace(input.Id))
        {
            var existing = await _recReqs.Find(x => x.Id == input.Id).FirstOrDefaultAsync(ct);
            if (existing != null) return (existing.Id, existing.Text);

            if (!string.IsNullOrWhiteSpace(input.Text))
            {
                var doc = new RecRequirement { Id = input.Id, Text = input.Text.Trim() };
                await _recReqs.InsertOneAsync(doc, cancellationToken: ct);
                return (doc.Id, doc.Text);
            }
            return null;
        }

        var text = input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var found = await _recReqs.Find(x => x.Text == text).FirstOrDefaultAsync(ct);
        if (found != null) return (found.Id, found.Text);

        var ins = new RecRequirement { Id = ObjectId.GenerateNewId().ToString(), Text = text };
        await _recReqs.InsertOneAsync(ins, cancellationToken: ct);
        return (ins.Id, ins.Text);
    }
}
