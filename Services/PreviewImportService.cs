using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.Data;            // MongoDbService
using CommentToGame.DTOs;
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

    private readonly IMongoCollection<Gallery> _galleries;

    public PreviewImportService(MongoDbService svc)
    {
        var db = svc.Database ?? throw new InvalidOperationException("Mongo database is null.");
        _games     = db.GetCollection<Game>("Games");
        _details   = db.GetCollection<Game_Details>("GameDetails");
        _genres    = db.GetCollection<Genre>("Genres");
        _platforms = db.GetCollection<Platform>("Platforms");
        _minReqs   = db.GetCollection<MinRequirement>("MinRequirements");
        _recReqs   = db.GetCollection<RecRequirement>("RecRequirements");

        _galleries = db.GetCollection<Gallery>("Galleries"); 
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
            .Set(x => x.isUpcoming, dto?.ReleaseDate > DateTime.Now || dto?.ReleaseDate == null )
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

            ub.SetOnInsert(x => x.GameId, game.Id),
            ub.SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString()),


            ub.SetOnInsert(x => x.GameDirector,            ""),                 // string → ""
            ub.SetOnInsert(x => x.ScenarioWriters,         new List<string>()), // list → []
            ub.SetOnInsert(x => x.ArtDirector,             ""),
            ub.SetOnInsert(x => x.LeadActors,              new List<string>()),
            ub.SetOnInsert(x => x.VoiceActors,             new List<string>()),
            ub.SetOnInsert(x => x.MusicComposer,           ""),
            ub.SetOnInsert(x => x.Cinematics_VfxTeam,      new List<string>()),

        };

        // DLCs: dto.Dlcs (List<DlcItemDto>) -> List<DLCitem>, mevcut fiyatı koru
if (dto.Dlcs != null) // null ise hiç dokunma; boş [] gelirse temizle
{
    // Mevcut detaydan DLC'leri çek (fiyat korumak için)
    var currentDlcBox = await _details
        .Find(detFilter)
        .Project(d => new { d.DLCs })
        .FirstOrDefaultAsync(ct);

    var existingByName = (currentDlcBox?.DLCs ?? new List<DLCitem>())
        .Where(d => !string.IsNullOrWhiteSpace(d.Name))
        .GroupBy(d => d.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    // gelen dto.Dlcs: List<GameMerge.DlcItemDto>
    var mappedDlcItems = dto.Dlcs
        .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Name))
        .GroupBy(d => d.Name!.Trim(), StringComparer.OrdinalIgnoreCase) // aynı isimleri tekle
        .Select(g =>
        {
            var name = g.Key;
            var first = g.First();
            // önce mevcut DB fiyatı, yoksa dto fiyatı, o da yoksa null
            double? priceToKeep = existingByName.TryGetValue(name, out var ex) && ex.Price.HasValue
                ? ex.Price
                : first.Price;

            return new DLCitem { Name = name, Price = priceToKeep };
        })
        .ToList();

    updates.Add(ub.Set(x => x.DLCs, mappedDlcItems));
}



        

            if (dto.Awards != null)
            updates.Add(ub.Set(x => x.Awards, dto.Awards));

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


        // ---------- 5) Gallery upsert ----------
        var imageDocs = (dto.Images ?? new List<GameMerge.ImageDto>())
    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Url))
    .GroupBy(i => i.Url!.Trim(), StringComparer.OrdinalIgnoreCase)
    .Select(g => g.First())
    .Select(i => new Image
    {
        Id = ObjectId.GenerateNewId().ToString(),
        URL = i.Url!.Trim(),
        // Başlığı şimdilik aynen al; boş ise null bırakıp sonra numaralandıracağız
        Title = string.IsNullOrWhiteSpace(i.Title) ? null : i.Title!.Trim(),
        MetaDatas = new List<MetaData>() // şimdilik boş
    })
    .ToList();

// 5.b) MAIN IMAGE: varsa "Main Image" olarak ekle/taşı (duplikeyi engelle)
var mainUrl = dto.MainImage?.Trim();
if (!string.IsNullOrWhiteSpace(mainUrl))
{
    var existedIdx = imageDocs.FindIndex(x => 
        string.Equals(x.URL, mainUrl, StringComparison.OrdinalIgnoreCase));

    if (existedIdx >= 0)
    {
        // Varsa: başa taşı ve adını "Main Image" yap
        var existingMain = imageDocs[existedIdx];   // <- isim değişti
        existingMain.Title = "Main Image";
        imageDocs.RemoveAt(existedIdx);
        imageDocs.Insert(0, existingMain);  
    }
    else
    {
        // Yoksa: yeni bir "Main Image" öğesi olarak en başa ekle
        imageDocs.Insert(0, new Image
        {
            Id = ObjectId.GenerateNewId().ToString(),
            URL = mainUrl,
            Title = "Main Image",
            MetaDatas = new List<MetaData>
            {
                new MetaData { Label = "Type", Value = "Main" }
            }
        });
    }
}

// 5.c) Başlıkları doldur: boş kalanlara "Screenshot N"
for (int i = 0; i < imageDocs.Count; i++)
{
    if (string.IsNullOrWhiteSpace(imageDocs[i].Title))
    {
        // 0. sıradaki zaten Main Image olabilir; hiç başlığı yoksa default verelim
        imageDocs[i].Title = $"Screenshot {i + 1}";
    }
}

// 5.d) VIDEOS: YouTubeId varsa kısa link; duplike temizle (yt:id / url:link); limit YOK
string ToVideoUrl(GameMerge.VideoDto v)
{
    if (!string.IsNullOrWhiteSpace(v.YouTubeId))
        return $"https://youtu.be/{v.YouTubeId!.Trim()}";
    return (v.Url ?? "").Trim();
}

var videoDocs = (dto.Videos ?? new List<GameMerge.VideoDto>())
    .Where(v => v != null && (!string.IsNullOrWhiteSpace(v.Url) || !string.IsNullOrWhiteSpace(v.YouTubeId)))
    .Select(v => new
    {
        Key = !string.IsNullOrWhiteSpace(v.YouTubeId)
            ? $"yt:{v.YouTubeId!.Trim()}"
            : $"url:{(v.Url ?? "").Trim()}",
        Url = ToVideoUrl(v),
        Title = v.Title
    })
    .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
    .Select(g => g.First())
    .Select((x, idx) => new Video
    {
        Id = ObjectId.GenerateNewId().ToString(),
        URL = x.Url,
        Title = string.IsNullOrWhiteSpace(x.Title) ? $"Trailer {idx + 1}" : x.Title!.Trim(),
        MetaDatas = new List<MetaData>() // şimdilik boş
    })
    .ToList();

// 5.e) Gallery: GameId bazlı upsert (tam listeyi set et)
var galFilter = Builders<Gallery>.Filter.Eq(g => g.GameId, game.Id);

// SetOnInsert ile temel alanları hazırla
var galUpdate = Builders<Gallery>.Update
    .SetOnInsert(g => g.Id, ObjectId.GenerateNewId().ToString())
    .SetOnInsert(g => g.GameId, game.Id);

// Tam listeyi atomik şekilde set et (tam eşitleme)
galUpdate = galUpdate.Set(g => g.Images, imageDocs);
galUpdate = galUpdate.Set(g => g.Videos, videoDocs);

        await _galleries.UpdateOneAsync(
            galFilter,
            galUpdate,
            new UpdateOptions { IsUpsert = true },
            ct
        );


    var mainImg = imageDocs
    .FirstOrDefault(i => string.Equals(i.Title, "Main Image", StringComparison.OrdinalIgnoreCase))
    ?? imageDocs.FirstOrDefault();

// 2) "Screenshot 1" görselini bul; yoksa main olmayan ilk görsel; o da yoksa main
var ss1Img = imageDocs.FirstOrDefault(i =>
                    !string.IsNullOrEmpty(i.Title) &&
                    i.Title.StartsWith("Screenshot 1", StringComparison.OrdinalIgnoreCase))
            ?? imageDocs.FirstOrDefault(i => mainImg == null
                                             || !string.Equals(i.URL, mainImg.URL, StringComparison.OrdinalIgnoreCase))
            ?? mainImg;

// 3) Video: "Trailer 1" yoksa ilk video
var tr1Vid = videoDocs.FirstOrDefault(v =>
                 string.Equals(v.Title, "Trailer 1", StringComparison.OrdinalIgnoreCase))
            ?? videoDocs.FirstOrDefault();

// 4) Mevcut Game dokümanında bu alanlar zaten dolu mu, bak
var existingLite = await _games.Find(g => g.Id == game.Id)
    .Project(g => new { g.Featured_Section_Background, g.Poster_Image, g.Poster_Video })
    .FirstOrDefaultAsync(ct);

// 5) Sadece boş olanları doldur (kullanıcı sonradan değiştirirse import tekrarında ezmeyelim)
var gub = Builders<Game>.Update;
var gsets = new List<UpdateDefinition<Game>>();

// === YENİ MANTIK ===
// Featured = Screenshot 1 (fallback: mainImg)
if (existingLite?.Featured_Section_Background == null && ss1Img != null)
{
    gsets.Add(gub.Set(x => x.Featured_Section_Background, new Image {
        Id = ObjectId.GenerateNewId().ToString(),
        URL = ss1Img.URL,
        Title = ss1Img.Title,
        MetaDatas = ss1Img.MetaDatas
    }));
}

// Poster = Main Image (fallback: ss1Img)
if (existingLite?.Poster_Image == null && mainImg != null)
{
    gsets.Add(gub.Set(x => x.Poster_Image, new Image {
        Id = ObjectId.GenerateNewId().ToString(),
        URL = mainImg.URL,
        Title = mainImg.Title,
        MetaDatas = mainImg.MetaDatas
    }));
}

// Poster video = Trailer 1 (fallback: ilk video)
if (existingLite?.Poster_Video == null && tr1Vid != null)
{
    gsets.Add(gub.Set(x => x.Poster_Video, new Video {
        Id = ObjectId.GenerateNewId().ToString(),
        URL = tr1Vid.URL,
        Title = tr1Vid.Title,
        MetaDatas = tr1Vid.MetaDatas
    }));
}

if (gsets.Count > 0)
{
    await _games.UpdateOneAsync(g => g.Id == game.Id, gub.Combine(gsets), cancellationToken: ct);
}







        if (dto.StoreLinks is { Count: > 0 })
            updates.Add(ub.Set(x => x.Store_Links, dto.StoreLinks));

        await _details.UpdateOneAsync(detFilter, ub.Combine(updates), new UpdateOptions { IsUpsert = true }, ct);

        
        

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
