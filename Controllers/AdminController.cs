using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.DTOs;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Globalization;
using CommentToGame.Services;  // ISystemLogger

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
    private readonly IMongoCollection<Gallery> _galleries;
    private readonly IConfiguration _config;
    private readonly ISystemLogger _logger;

    public AdminController(MongoDbService service, IConfiguration config, ISystemLogger logger)
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
        _galleries = db.GetCollection<Gallery>("Galleries");

        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---------------- Dashboard / Raporlar ----------------

    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin")]
    public IActionResult Dashboard()
    {
        _ = _logger.InfoAsync(SystemLogCategory.System, "Admin dashboard viewed", User?.Identity?.Name ?? "unknown");
        return Ok("Admin panel verisi 🔐");
    }

    [HttpGet("usergamecount")]
    [Authorize] // istersen Roles="Admin"
    public async Task<IActionResult> UserGameCount([FromQuery] int windowDays = 7, CancellationToken ct = default)
    {
        if (windowDays <= 0) windowDays = 7;

        var totalUsers = await _users.CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: ct);
        var totalGames = await _games.CountDocumentsAsync(FilterDefinition<Game>.Empty, cancellationToken: ct);

        var todayUtc = DateTime.UtcNow.Date;
        var currEnd = todayUtc.AddDays(1);          // [currStart, currEnd)
        var currStart = currEnd.AddDays(-windowDays);
        var prevEnd = currStart;                    // [prevStart, prevEnd)
        var prevStart = prevEnd.AddDays(-windowDays);

        var (uCurr, uPrev) = await CountWindowPairAsync(_users, currStart, currEnd, prevStart, prevEnd, ct);
        var (gCurr, gPrev) = await CountWindowPairAsync(_games, currStart, currEnd, prevStart, prevEnd, ct);

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

    /// <summary>
    /// GET /api/admin/growth?from=2025-08-01&to=2025-08-25&mode=daily|cumulative
    /// Dönüş: [{ date: 'yyyy-MM-dd', users: n, games: n }]
    /// </summary>
    [HttpGet("growth")]
    [Authorize] // istersen Roles="Admin"
    public async Task<IActionResult> Growth([FromQuery] string from, [FromQuery] string to, [FromQuery] string? mode = "cumulative", CancellationToken ct = default)
    {
        if (!TryParseDay(from, out var fromDay) || !TryParseDay(to, out var toDay))
            return BadRequest(new { message = "from/to 'yyyy-MM-dd' formatında olmalı." });

        var start = fromDay.Date;
        var endExclusive = toDay.Date.AddDays(1);

        var usersDaily = await AggregateDailyCountsAsync(_users, start, endExclusive, ct);
        var gamesDaily = await AggregateDailyCountsAsync(_games, start, endExclusive, ct);

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

        var modeNorm = (mode ?? "cumulative").Trim().ToLowerInvariant();
        if (modeNorm == "cumulative")
        {
            int cu = 0, cg = 0;
            list = list.Select(x => { cu += x.u; cg += x.g; return (x.day, cu, cg); }).ToList();
        }


        var payload = list.Select(x => new { date = x.day, users = x.u, games = x.g }).ToList();
        return Ok(payload);
    }

    // ---------------- Games ----------------

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
                genreDict[gr.Id] = gr.Name;
        }

        // ---- PLATFORMS
        var allPlatformIds = details.Where(d => d.PlatformIds != null)
                                    .SelectMany(d => d.PlatformIds!)
                                    .Distinct()
                                    .ToList();

        var platformDict = new Dictionary<string, string>();
        if (allPlatformIds.Count > 0)
        {
            var plats = await _platforms.Find(p => allPlatformIds.Contains(p.Id)).ToListAsync();
            foreach (var p in plats)
                platformDict[p.Id] = p.Name;
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
                Developer = det?.Developer ?? g.Studio,
                Genres = genreNames,
                Platforms = platformNames,
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
        try
        {
            var game = await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
            if (game == null)
            {
                await _logger.WarningAsync(SystemLogCategory.GameManagement, $"DeleteGame failed (not found) id={id}", User?.Identity?.Name ?? "admin");
                return NotFound(new { message = "Game not found" });
            }

            var details = await _details.Find(d => d.GameId == id).FirstOrDefaultAsync(ct);
            var minReqId = details?.MinRequirementId;
            var recReqId = details?.RecRequirementId;

            await _galleries.DeleteManyAsync(g => g.GameId == id, ct);
            await _details.DeleteManyAsync(d => d.GameId == id, ct);
            await _games.DeleteOneAsync(g => g.Id == id, ct);

            if (!string.IsNullOrWhiteSpace(minReqId))
            {
                var inUseElsewhere = await _details.Find(d => d.MinRequirementId == minReqId).Limit(1).AnyAsync(ct);
                if (!inUseElsewhere) await _minReqs.DeleteOneAsync(x => x.Id == minReqId, ct);
            }
            if (!string.IsNullOrWhiteSpace(recReqId))
            {
                var inUseElsewhere = await _details.Find(d => d.RecRequirementId == recReqId).Limit(1).AnyAsync(ct);
                if (!inUseElsewhere) await _recReqs.DeleteOneAsync(x => x.Id == recReqId, ct);
            }

            await _logger.SuccessAsync(SystemLogCategory.GameManagement, $"Game deleted id={id} title={game.Game_Name}", User?.Identity?.Name ?? "admin");
            return Ok(new { message = $"Game {id} deleted (details, gallery and unused requirements removed)" });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(SystemLogCategory.System, $"DeleteGame error id={id}: {ex.Message}", User?.Identity?.Name ?? "admin");
            throw;
        }
    }

    [HttpGet("games/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GameDetailDto>> GetGameById(string id, CancellationToken ct)
    {
        var game = await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
        if (game == null)
        {
            await _logger.WarningAsync(SystemLogCategory.GameManagement, $"GetGameById not found id={id}", User?.Identity?.Name ?? "admin");
            return NotFound(new { message = "Game not found" });
        }

        var details = await _details.Find(d => d.GameId == id).FirstOrDefaultAsync(ct);
        var gallery = await _galleries.Find(g => g.GameId == id).FirstOrDefaultAsync(ct);

        var imagesDto = (gallery?.Images ?? new List<Image>())
            .Where(i => !string.IsNullOrWhiteSpace(i.URL))
            .Select(i => new ImageDto
            {
                Url = i.URL,
                Title = string.IsNullOrWhiteSpace(i.Title) ? "Screenshot" : i.Title,
                MetaDatas = i.MetaDatas?.Select(m => new MetaDataDto { Label = m.Label, Value = m.Value }).ToList() ?? new List<MetaDataDto>()
            })
            .ToList();

        var videosDto = (gallery?.Videos ?? new List<Video>())
            .Where(v => !string.IsNullOrWhiteSpace(v.URL))
            .Select(v => new VideoDto
            {
                Url = v.URL,
                Title = string.IsNullOrWhiteSpace(v.Title) ? "Trailer" : v.Title,
                YouTubeId = null,
                MetaDatas = v.MetaDatas?.Select(m => new MetaDataDto { Label = m.Label, Value = m.Value }).ToList() ?? new List<MetaDataDto>()
            })
            .ToList();

        // GENRES
        var genreNames = new List<string>();
        if (details?.GenreIds != null && details.GenreIds.Count > 0)
        {
            var genres = await _genres.Find(g => details.GenreIds.Contains(g.Id)).ToListAsync(ct);
            genreNames = genres.Select(g => g.Name).ToList();
        }

        // PLATFORMS
        var platformNames = new List<string>();
        if (details?.PlatformIds != null && details.PlatformIds.Count > 0)
        {
            var plats = await _platforms.Find(p => details.PlatformIds.Contains(p.Id)).ToListAsync(ct);
            platformNames = plats.Select(p => p.Name).ToList();
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

            TimeToBeat_Hastily = details?.TimeToBeat_Hastily,
            TimeToBeat_Normally = details?.TimeToBeat_Normally,
            TimeToBeat_Completely = details?.TimeToBeat_Completely,

            MinRequirements = minText,
            RecRequirements = recText,
            ContentWarnings = details?.Content_Warnings ?? new List<string>(),
            AgeRatings = details?.Age_Ratings ?? new List<string>(),
            AudioLanguages = details?.Audio_Language ?? new List<string>(),
            SubtitleLanguages = details?.Subtitles ?? new List<string>(),
            InterfaceLanguages = details?.Interface_Language ?? new List<string>(),
            Soundtrack = game.Soundtrack ?? new List<string>(),
            Images = imagesDto,
            Videos = videosDto,
            Gallery = new GalleryDto { Images = imagesDto, Videos = videosDto },

            StoreLinks = (details?.Store_Links ?? new List<StoreLink>())
                .Select(s => new StoreLinkDto
                {
                    StoreId = s.StoreId,
                    Store = s.Store,
                    Slug = s.Slug,
                    Domain = s.Domain,
                    Url = s.Url,
                    ExternalId = s.ExternalId
                })
                .ToList(),

            Featured_Section_Background = ToImageDto(game.Featured_Section_Background),
            Poster_Image = ToImageDto(game.Poster_Image),
            Poster_Video = ToVideoDto(game.Poster_Video)
        };

        return Ok(dto);
    }

    [HttpPut("games/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateGame(string id, [FromBody] GameDetailDto dto, CancellationToken ct)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Invalid payload: 'title' is required." });

        try
        {
            var game = await _games.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
            if (game == null)
            {
                await _logger.WarningAsync(SystemLogCategory.GameManagement, $"UpdateGame not found id={id}", User?.Identity?.Name ?? "admin");
                return NotFound(new { message = "Game not found" });
            }

            game.Game_Name = dto.Title;
            game.Release_Date = dto.ReleaseDate;
            game.Studio = dto.Studio;
            game.GgDb_Rating = dto.GgdbRating;
            game.Metacritic_Rating = dto.MetacriticRating;
            game.Main_image_URL = dto.Cover;
            game.Main_video_URL = dto.Video;
            game.Crew = dto.Crew ?? new List<string>();
            game.Soundtrack = dto.Soundtrack ?? new List<string>();

            if (dto.Featured_Section_Background != null)
                game.Featured_Section_Background = MapImageDto(dto.Featured_Section_Background, game.Featured_Section_Background);
            if (dto.Poster_Image != null)
                game.Poster_Image = MapImageDto(dto.Poster_Image, game.Poster_Image);
            if (dto.Poster_Video != null)
                game.Poster_Video = MapVideoDto(dto.Poster_Video, game.Poster_Video);

            await _games.ReplaceOneAsync(g => g.Id == id, game, cancellationToken: ct);

            var details = await _details.Find(d => d.GameId == id).FirstOrDefaultAsync(ct)
                          ?? new Game_Details { GameId = id };

            details.Developer = dto.Developer;
            details.Publisher = dto.Publisher;
            details.Story = dto.Story;
            details.Tags = dto.Tags ?? new List<string>();
            details.DLCs = dto.Dlcs ?? new List<string>();
            details.Awards = dto.Awards;
            details.Engines = dto.GameEngine ?? new List<string>();

            if (dto.GameDirector != null) details.GameDirector = NormStr(dto.GameDirector);
            if (dto.Writers != null) details.ScenarioWriters = NormList(dto.Writers);
            if (dto.ArtDirector != null) details.ArtDirector = NormStr(dto.ArtDirector);
            if (dto.LeadActors != null) details.LeadActors = NormList(dto.LeadActors);
            if (dto.VoiceActors != null) details.VoiceActors = NormList(dto.VoiceActors);
            if (dto.MusicComposer != null) details.MusicComposer = NormStr(dto.MusicComposer);
            if (dto.CinematicsVfxTeam != null) details.Cinematics_VfxTeam = NormList(dto.CinematicsVfxTeam);

            details.GameDirector ??= "";
            details.ScenarioWriters ??= new List<string>();
            details.ArtDirector ??= "";
            details.LeadActors ??= new List<string>();
            details.VoiceActors ??= new List<string>();
            details.MusicComposer ??= "";
            details.Cinematics_VfxTeam ??= new List<string>();

            details.TimeToBeat_Hastily = dto.TimeToBeat_Hastily;
            details.TimeToBeat_Normally = dto.TimeToBeat_Normally;
            details.TimeToBeat_Completely = dto.TimeToBeat_Completely;

            details.Content_Warnings = dto.ContentWarnings ?? new List<string>();
            details.Age_Ratings = dto.AgeRatings ?? new List<string>();
            details.Audio_Language = dto.AudioLanguages ?? new List<string>();
            details.Subtitles = dto.SubtitleLanguages ?? new List<string>();
            details.Interface_Language = dto.InterfaceLanguages ?? new List<string>();
            details.Screenshots = dto.Screenshots ?? new List<string>();
            details.Trailers = dto.Trailers ?? new List<TrailerDto>();

            var hasIncomingMedia = (dto.Images != null && dto.Images.Count > 0)
                                   || (dto.Videos != null && dto.Videos.Count > 0);

            if (hasIncomingMedia)
            {
                var existingGallery = await _galleries.Find(g => g.GameId == id).FirstOrDefaultAsync(ct)
                                     ?? new Gallery { Id = ObjectId.GenerateNewId().ToString(), GameId = id };

                existingGallery.Images = (dto.Images ?? new List<ImageDto>())
                    .Where(i => !string.IsNullOrWhiteSpace(i.Url))
                    .Select(i => new Image
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        URL = i.Url.Trim(),
                        Title = string.IsNullOrWhiteSpace(i.Title) ? "Screenshot" : i.Title.Trim(),
                        MetaDatas = (i.MetaDatas ?? new List<MetaDataDto>())
                            .Select(m => new MetaData { Label = m.Label, Value = m.Value })
                            .ToList()
                    })
                    .ToList();

                existingGallery.Videos = (dto.Videos ?? new List<VideoDto>())
                    .Where(v => !string.IsNullOrWhiteSpace(v.Url))
                    .Select(v => new Video
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        URL = v.Url.Trim(),
                        Title = string.IsNullOrWhiteSpace(v.Title) ? "Trailer" : v.Title.Trim(),
                        MetaDatas = (v.MetaDatas ?? new List<MetaDataDto>())
                            .Select(m => new MetaData { Label = m.Label, Value = m.Value })
                            .ToList()
                    })
                    .ToList();

                await _galleries.ReplaceOneAsync(
                    g => g.GameId == id,
                    existingGallery,
                    new ReplaceOptions { IsUpsert = true },
                    ct
                );
            }

            if (dto.MinRequirements is { Length: > 0 } minText)
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
                    var doc = new MinRequirement { Text = minText };
                    await _minReqs.InsertOneAsync(doc, cancellationToken: ct);
                    details.MinRequirementId = doc.Id;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(details.MinRequirementId))
                {
                    try { await _minReqs.DeleteOneAsync(x => x.Id == details.MinRequirementId, ct); } catch { }
                }
                details.MinRequirementId = null;
            }

            if (dto.RecRequirements is { Length: > 0 } recText)
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
                    try { await _recReqs.DeleteOneAsync(x => x.Id == details.RecRequirementId, ct); } catch { }
                }
                details.RecRequirementId = null;
            }

            if (dto.Genres != null)
            {
                var genreDocs = await _genres
                    .Find(g => dto.Genres.Contains(g.Name))
                    .Project(g => new { g.Id, g.Name })
                    .ToListAsync(ct);

                details.GenreIds = genreDocs.Select(x => x.Id).ToList();
            }

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
                   StoreId = s.StoreId ?? 0,
                   Store = s.Store ?? "",
                   Slug = s.Slug ?? "",
                   Domain = s.Domain ?? "",
                   Url = s.Url ?? "",
                   ExternalId = s.ExternalId
               })
               .ToList();

            await _details.ReplaceOneAsync(d => d.GameId == id, details, new ReplaceOptions { IsUpsert = true }, ct);

            await _logger.SuccessAsync(SystemLogCategory.GameManagement, $"Game updated id={id} title={dto.Title}", User?.Identity?.Name ?? "admin");
            return Ok(new { message = $"Game {id} updated" });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(SystemLogCategory.System, $"UpdateGame error id={id}: {ex.Message}", User?.Identity?.Name ?? "admin");
            throw;
        }
    }

    [HttpPost("approve/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(string id)
    {
        await _logger.SuccessAsync(SystemLogCategory.GameManagement, $"Game {id} approved", User?.Identity?.Name ?? "admin");
        return Ok();
    }

    // ---------------- Users ----------------

    [HttpGet("getUsers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await _users.Find(_ => true).ToListAsync();
        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            Password = string.Empty,
            Birthdate = u.Birthdate,
            Country = u.Country,
            ProfileImageUrl = u.ProfileImageUrl,
            UserType = u.UserType,
            isBanned = u.isBanned
        }).ToList();

        return Ok(userDtos);
    }

    [HttpDelete("deleteUser/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken ct)
    {
        var user = await _users.Find(g => g.Id == id).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            await _logger.WarningAsync(SystemLogCategory.UserActions, $"DeleteUser failed (not found) id={id}", User?.Identity?.Name ?? "admin");
            return NotFound(new { message = "User not found" });
        }

        await _users.DeleteOneAsync(g => g.Id == id, ct);
        await _logger.SuccessAsync(SystemLogCategory.UserActions, $"User deleted id={id} name={user.UserName}", User?.Identity?.Name ?? "admin");
        return Ok(new { message = $"User {id} deleted" });
    }
public sealed record BanRequest(bool IsBanned);

[HttpPatch("{id}/ban")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> BanUser(string id, [FromBody] BanRequest req, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(id))
        return BadRequest(new { message = "id required" });

    // (Opsiyonel) Kendini banlama koruması
    var adminId = User.FindFirst("sub")?.Value;
    if (!string.IsNullOrEmpty(adminId) && adminId == id)
        return BadRequest(new { message = "You cannot ban yourself." });

    var filter = Builders<User>.Filter.Eq(u => u.Id, id); 
    var update = Builders<User>.Update.Set(u => u.isBanned, req.IsBanned);

    var options = new FindOneAndUpdateOptions<User, User>
    {
        IsUpsert = false,
        ReturnDocument = ReturnDocument.After,
        Projection = Builders<User>.Projection
            .Include(u => u.Id)
            .Include(u => u.UserName)
            .Include(u => u.isBanned)
    };

    var updated = await _users.FindOneAndUpdateAsync(filter, update, options, ct);
    if (updated is null)
    {
        await _logger.WarningAsync(SystemLogCategory.UserActions, $"User id: {id} is not found.");
        return NotFound(new { message = "User not found." });
    }

    var action = req.IsBanned ? "banned" : "unbanned";
    await _logger.SuccessAsync(SystemLogCategory.UserActions, $"User id: {id} {action}.");

    return Ok(new { ok = true, id = updated.Id, isBanned = updated.isBanned });
}


    public sealed class UpdateUserRoleInput
    {
        public string Role { get; set; } = ""; // "User" | "Admin"
    }

    [HttpPatch("{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserRole(string id, [FromBody] UpdateUserRoleInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "id required" });

        if (!Enum.TryParse<UserType>(input.Role, true, out var ut))
        {
            await _logger.WarningAsync(SystemLogCategory.UserActions, $"UpdateUserRole invalid role='{input.Role}' id={id}", User?.Identity?.Name ?? "admin");
            return BadRequest(new { message = "Role must be 'User' or 'Admin'." });
        }

        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        var update = Builders<User>.Update.Set(u => u.UserType, ut);

        var res = await _users.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (res.MatchedCount == 0)
        {
            await _logger.WarningAsync(SystemLogCategory.UserActions, $"UpdateUserRole not found id={id}", User?.Identity?.Name ?? "admin");
            return NotFound(new { message = "User not found." });
        }

        await _logger.SuccessAsync(SystemLogCategory.UserActions, $"UpdateUserRole id={id} role={ut}", User?.Identity?.Name ?? "admin");
        return Ok(new { ok = true, id, userType = ut.ToString() });
    }

    // ---------------- Helpers ----------------

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

    private static bool TryParseDay(string s, out DateTime dayUtc)
    {
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

    private static Image MapImageDto(ImageDto dto, Image? existing = null)
    {
        var id = existing?.Id ?? ObjectId.GenerateNewId().ToString();
        return new Image
        {
            Id = id,
            URL = (dto.Url ?? "").Trim(),
            Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
            MetaDatas = (dto.MetaDatas ?? new List<MetaDataDto>())
                .Select(m => new MetaData { Label = m.Label, Value = m.Value })
                .ToList()
        };
    }

    private static Video MapVideoDto(VideoDto dto, Video? existing = null)
    {
        var id = existing?.Id ?? ObjectId.GenerateNewId().ToString();
        return new Video
        {
            Id = id,
            URL = (dto.Url ?? "").Trim(),
            Title = string.IsNullOrWhiteSpace(dto.Title) ? "Trailer" : dto.Title.Trim(),
            MetaDatas = (dto.MetaDatas ?? new List<MetaDataDto>())
                .Select(m => new MetaData { Label = m.Label, Value = m.Value })
                .ToList()
        };
    }

    private static ImageDto? ToImageDto(Image? img)
    {
        if (img == null || string.IsNullOrWhiteSpace(img.URL)) return null;
        return new ImageDto
        {
            Url = img.URL,
            Title = string.IsNullOrWhiteSpace(img.Title) ? "" : img.Title!,
            MetaDatas = (img.MetaDatas ?? new List<MetaData>())
                .Select(m => new MetaDataDto { Label = m.Label, Value = m.Value })
                .ToList()
        };
    }

    private static VideoDto? ToVideoDto(Video? vid)
    {
        if (vid == null || string.IsNullOrWhiteSpace(vid.URL)) return null;
        return new VideoDto
        {
            Url = vid.URL,
            Title = string.IsNullOrWhiteSpace(vid.Title) ? "Trailer" : vid.Title!,
            YouTubeId = null, // DB modelinde varsa ExtractYouTubeId ile doldurulabilir
            MetaDatas = (vid.MetaDatas ?? new List<MetaData>())
                .Select(m => new MetaDataDto { Label = m.Label, Value = m.Value })
                .ToList()
        };
    }

    private static string NormStr(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

    private static List<string> NormList(IEnumerable<string>? xs)
        => xs?.Where(x => !string.IsNullOrWhiteSpace(x))
              .Select(x => x.Trim())
              .Distinct(StringComparer.OrdinalIgnoreCase)
              .ToList()
           ?? new List<string>();
           

           // Koleksiyon için gün bazında sayım yapar: [{ day: "yyyy-MM-dd", count: n }]
private static async Task<List<(string day, int count)>> AggregateDailyCountsAsync<TDoc>(
    IMongoCollection<TDoc> col,
    DateTime startInclusive,
    DateTime endExclusive,
    CancellationToken ct)
{
    // Tarih alanı coalesce: Createdat > CreatedAt > CreatedDate > createdAt > ObjectId ts
    var coalesceCreated = new BsonDocument("$ifNull", new BsonArray {
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

}
