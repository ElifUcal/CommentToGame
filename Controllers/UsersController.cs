using System.Security.Claims;
using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.DTOs;
using CommentToGame.enums;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<GameLike> _gameLikes;
    private readonly IMongoCollection<GameDislike> _gameDislikes;
    private readonly IMongoCollection<GameFavorite> _gameFavorites;
    private readonly IMongoCollection<GamePlantoPlay> _gamePlans;
    private readonly IMongoCollection<GameProgress> _gameProgresses;

    private readonly IMongoCollection<Reviews> _reviews;
    private readonly IMongoCollection<GameRating> _gameRatings;
    private readonly IMongoCollection<Game> _games;

    private readonly IMongoCollection<Game_Details> _gameDetails;
    private readonly IMongoCollection<Genre> _genres;

    private readonly IGamificationService _gamification;

    
    public UsersController(MongoDbService db, IGamificationService gamification)
    {
        _users = db.GetCollection<User>("User"); // koleksiyon adın neyse
        _gameLikes      = db.GetCollection<GameLike>("game_likes");
        _gameDislikes   = db.GetCollection<GameDislike>("game_dislikes");
        _gameFavorites  = db.GetCollection<GameFavorite>("game_favorites");
        _gamePlans      = db.GetCollection<GamePlantoPlay>("game_planstoplay");
        _gameProgresses = db.GetCollection<GameProgress>("game_progress");

        _reviews        = db.GetCollection<Reviews>("reviews");
        _gameRatings    = db.GetCollection<GameRating>("GameRatings");
        _games          = db.GetCollection<Game>("Games");
        _gameDetails    = db.GetCollection<Game_Details>("GameDetails");
        _genres         = db.GetCollection<Genre>("Genres");
        _gamification   = gamification;
    }





    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var users = await _users
            .Find(_ => true)
            .Project(u => new
            {
                id = u.Id,
                userName = u.UserName,
                email = u.Email,
                country = u.Country,
                profileImageUrl = u.ProfileImageUrl,
                userType = u.UserType,
                isBanned = u.isBanned
            })
            .ToListAsync(ct);

        return Ok(new { users });
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id, CancellationToken ct)
    {
        var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (user == null) return NotFound();

        return Ok(new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Password = string.Empty,
            Birthdate = user.Birthdate,
            Country = user.Country,
            ProfileImageUrl = user.ProfileImageUrl,
            UserType = user.UserType,
            isBanned = user.isBanned
        });
    }


    [HttpGet("basic")]
    public async Task<IActionResult> GetBasic([FromQuery] string ids, CancellationToken ct)
    {
        var arr = (ids ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (arr.Count == 0)
            return Ok(new { users = Array.Empty<object>() });

        var users = await _users.Find(u => arr.Contains(u.Id))
            .Project(u => new
            {
                id = u.Id,
                userName = u.UserName,
                profileImageUrl = u.ProfileImageUrl
            })
            .ToListAsync(ct);

        return Ok(new { users });
    }


   [Authorize]
[HttpPatch("{id:length(24)}")]
[Consumes("application/json")]
[Produces("application/json")]
public async Task<IActionResult> Patch(string id, [FromBody] UpdateUserDto dto, CancellationToken ct)
{
    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        return BadRequest("Geçersiz id.");

    if (dto is null)
        return BadRequest("Body boş veya Content-Type hatalı. Lütfen application/json gönderin.");

    var requesterId = GetUserIdFromClaims(User);
    var isAdmin = User.IsInRole("Admin");

    if (string.IsNullOrEmpty(requesterId))
        return Unauthorized();

    if (!isAdmin && requesterId != id)
        return Forbid();

    var existing = await _users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
    if (existing == null) return NotFound();

    // --- XP için eski durum flag'leri (önceki state) ---
    var hadProfileImage = !string.IsNullOrEmpty(existing.ProfileImageUrl);
    var hadAbout        = !string.IsNullOrWhiteSpace(existing.About);
    var hadLocation     = !string.IsNullOrWhiteSpace(existing.Country)
                          && !string.IsNullOrWhiteSpace(existing.City);
    var hadGenres       = existing.FavoriteGenres != null && existing.FavoriteGenres.Any();
    var hadPlatforms    = existing.Platforms != null && existing.Platforms.Any();

    var b = Builders<User>.Update;
    var updates = new List<UpdateDefinition<User>>();

    // --- Email kontrolü ---
    if (dto.Email != null)
    {
        var newEmail = dto.Email.Trim().ToLowerInvariant();

        var existsEmail = await _users.Find(u => u.Email == newEmail && u.Id != id)
                                      .AnyAsync(ct);

        if (existsEmail)
            return BadRequest("Bu email başka bir kullanıcı tarafından kullanılıyor.");

        updates.Add(b.Set(x => x.Email, newEmail));
    }

    // --- UserName kontrolü ---
    if (dto.UserName != null)
    {
        var newUserName = dto.UserName.Trim();

        var existsUserName = await _users
            .Find(u => u.UserName == newUserName && u.Id != id)
            .AnyAsync(ct);

        if (existsUserName)
            return BadRequest("Bu kullanıcı adı başka bir kullanıcı tarafından kullanılıyor.");

        updates.Add(b.Set(x => x.UserName, newUserName));
    }

    // --- Diğer alanlar ---
    if (dto.Birthdate.HasValue)       updates.Add(b.Set(x => x.Birthdate, dto.Birthdate.Value));
    if (dto.Country != null)          updates.Add(b.Set(x => x.Country, dto.Country));
    if (dto.City != null)             updates.Add(b.Set(x => x.City, dto.City));
    if (dto.ProfileImageUrl != null)  updates.Add(b.Set(x => x.ProfileImageUrl, dto.ProfileImageUrl));
    if (dto.BannerUrl != null)        updates.Add(b.Set(x => x.BannerUrl, dto.BannerUrl));
    if (dto.About != null)            updates.Add(b.Set(x => x.About, dto.About));
    if (dto.FavoriteGenres != null)   updates.Add(b.Set(x => x.FavoriteGenres, dto.FavoriteGenres));
    if (dto.Platforms != null)        updates.Add(b.Set(x => x.Platforms, dto.Platforms));
    if (dto.Badge != null)            updates.Add(b.Set(x => x.Badge, dto.Badge));
    if (dto.Title != null)            updates.Add(b.Set(x => x.Title, dto.Title));
    if (dto.ContactUrl != null)       updates.Add(b.Set(x => x.ContactUrl, dto.ContactUrl));
    if (dto.Skills != null)           updates.Add(b.Set(x => x.Skills, dto.Skills));
    if (dto.Experiences != null)      updates.Add(b.Set(x => x.Experiences, dto.Experiences));
    if (dto.Projects != null)         updates.Add(b.Set(x => x.Projects, dto.Projects));
    if (dto.Educations != null)       updates.Add(b.Set(x => x.Educations, dto.Educations));
    if (dto.Awards != null)           updates.Add(b.Set(x => x.Awards, dto.Awards));
    if (dto.Name != null)             updates.Add(b.Set(x => x.Name, dto.Name));
    if (dto.Surname != null)          updates.Add(b.Set(x => x.Surname, dto.Surname));
    if (dto.FavConsoles != null)      updates.Add(b.Set(x => x.FavConsoles, dto.FavConsoles));
    if (dto.Equipment != null)        updates.Add(b.Set(x => x.Equipment, dto.Equipment));
    if (dto.CareerGoal != null)       updates.Add(b.Set(x => x.CareerGoal, dto.CareerGoal));

    if (updates.Count == 0)
        return BadRequest("Güncellenecek bir alan gönderilmedi.");

    // --- DB update ---
    var res = await _users.UpdateOneAsync(x => x.Id == id, b.Combine(updates), cancellationToken: ct);
    if (res.MatchedCount == 0) return NotFound();

    // Eğer hiçbir şey değişmemişse XP de verme
    if (res.ModifiedCount == 0)
        return Ok();

    // --- XP için yeni state'i dto üzerinden kontrol et (SIRALI, PARALLEL DEĞİL!) ---

    // 1) Profil fotoğrafı (ilk kez doluyorsa)
    var willHaveProfileImage =
        dto.ProfileImageUrl != null && !string.IsNullOrWhiteSpace(dto.ProfileImageUrl);

    if (!hadProfileImage && willHaveProfileImage)
    {
        await _gamification.AddXpAsync(
            userId: id,
            sourceType: XpSourceType.ProfileAvatarUploaded,
            sourceId: null,
            uniqueKey: $"user:{id}:profile-avatar"
        );
    }

    // 2) About/Bio (ilk kez doluyorsa)
    var willHaveAbout =
        dto.About != null && !string.IsNullOrWhiteSpace(dto.About);

    if (!hadAbout && willHaveAbout)
    {
        await _gamification.AddXpAsync(
            userId: id,
            sourceType: XpSourceType.ProfileBioCompleted,
            sourceId: null,
            uniqueKey: $"user:{id}:profile-bio"
        );
    }

    // 3) Location (ülke/şehir ilk kez set edilirse)
    var willHaveLocation =
        (!string.IsNullOrWhiteSpace(dto.Country) || !string.IsNullOrWhiteSpace(dto.City));

    if (!hadLocation && willHaveLocation)
    {
        await _gamification.AddXpAsync(
            userId: id,
            sourceType: XpSourceType.ProfileLocationCompleted,
            sourceId: null,
            uniqueKey: $"user:{id}:profile-location"
        );
    }

    // 4) Favori türler (ilk kez doluyorsa)
    var willHaveGenres =
        dto.FavoriteGenres != null && dto.FavoriteGenres.Any();

    if (!hadGenres && willHaveGenres)
    {
        await _gamification.AddXpAsync(
            userId: id,
            sourceType: XpSourceType.ProfileFavoriteGenresCompleted,
            sourceId: null,
            uniqueKey: $"user:{id}:profile-genres"
        );
    }

    // 5) Platformlar (ilk kez doluyorsa)
    var willHavePlatforms =
        dto.Platforms != null && dto.Platforms.Any();

    if (!hadPlatforms && willHavePlatforms)
    {
        await _gamification.AddXpAsync(
            userId: id,
            sourceType: XpSourceType.ProfilePlatformsCompleted,
            sourceId: null,
            uniqueKey: $"user:{id}:profile-platforms"
        );
    }

    return Ok();
}



[Authorize]
[HttpPost("{id:length(24)}/profile-image")]
[RequestSizeLimit(2 * 1024 * 1024)] // max 2 MB
public async Task<IActionResult> UploadProfileImage(
    string id,
    IFormFile file,
    CancellationToken ct)
{
    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        return BadRequest("Geçersiz id.");

    if (file == null || file.Length == 0)
        return BadRequest("Dosya gönderilmedi.");

    var requesterId = GetUserIdFromClaims(User);
    var isAdmin = User.IsInRole("Admin");

    if (string.IsNullOrEmpty(requesterId))
        return Unauthorized();

    if (!isAdmin && requesterId != id)
        return Forbid();

    var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
    if (user == null)
        return NotFound("Kullanıcı bulunamadı.");

    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExt.Contains(ext))
        return BadRequest("Sadece JPG, PNG veya WEBP yükleyebilirsiniz.");

    var webRoot = Directory.GetCurrentDirectory();
    var wwwrootPath = Path.Combine(webRoot, "wwwroot");
    var uploadsRoot = Path.Combine(wwwrootPath, "uploads", "profiles");
    Directory.CreateDirectory(uploadsRoot);

    var fileName = $"{id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
    var filePath = Path.Combine(uploadsRoot, fileName);

    using (var stream = System.IO.File.Create(filePath))
    {
        await file.CopyToAsync(stream, ct);
    }

    var baseUrl = $"{Request.Scheme}://{Request.Host}";
    var url = $"{baseUrl}/uploads/profiles/{fileName}";

    var update = Builders<User>.Update.Set(x => x.ProfileImageUrl, url);
    await _users.UpdateOneAsync(u => u.Id == id, update, cancellationToken: ct);

    // XP: profil fotoğrafı ilk kez yükleniyorsa verilecek (uniqueKey bunu garanti ediyor)
    await _gamification.AddXpAsync(
        userId: id,
        sourceType: XpSourceType.ProfileAvatarUploaded,
        sourceId: null,
        uniqueKey: $"user:{id}:profile-avatar"
    );

    return Ok(new { profileImageUrl = url });
}



[HttpGet("{id:length(24)}/activity")]
public async Task<ActionResult<UserActivityResponseDto>> GetUserActivity(
    string id,
    [FromQuery] int limit = 50,
    CancellationToken ct = default)
{
    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        return BadRequest("Geçersiz id.");

    var exists = await _users.Find(u => u.Id == id).AnyAsync(ct);
    if (!exists)
        return NotFound("Kullanıcı bulunamadı.");

    if (limit <= 0) limit = 50;
    if (limit > 200) limit = 200;

    // 1) Like / Dislike / Favorite / PlanToPlay / Progress
    var likesTask      = _gameLikes.Find(x => x.UserId == id).SortByDescending(x => x.CreatedAt).Limit(200).ToListAsync(ct);
    var dislikesTask   = _gameDislikes.Find(x => x.UserId == id).SortByDescending(x => x.CreatedAt).Limit(200).ToListAsync(ct);
    var favoritesTask  = _gameFavorites.Find(x => x.UserId == id).SortByDescending(x => x.CreatedAt).Limit(200).ToListAsync(ct);
    var plansTask      = _gamePlans.Find(x => x.UserId == id).SortByDescending(x => x.CreatedAt).Limit(200).ToListAsync(ct);
    var progressTask   = _gameProgresses.Find(x => x.UserId == id).SortByDescending(x => x.CreatedAt).Limit(200).ToListAsync(ct);

    // 2) Review ve Rating
    var reviewsTask    = _reviews.Find(x => x.UserId == id).SortByDescending(x => x.TodayDate).Limit(200).ToListAsync(ct);
    var ratingsTask    = _gameRatings.Find(x => x.UserId == id).SortByDescending(x => x.UpdatedAt).Limit(200).ToListAsync(ct);

    await Task.WhenAll(likesTask, dislikesTask, favoritesTask, plansTask, progressTask, reviewsTask, ratingsTask);

    var likes      = likesTask.Result;
    var dislikes   = dislikesTask.Result;
    var favorites  = favoritesTask.Result;
    var plans      = plansTask.Result;
    var progresses = progressTask.Result;
    var reviews    = reviewsTask.Result;
    var ratings    = ratingsTask.Result;

    // Kullanılan tüm GameId'leri topla
    var gameIds = likes.Select(x => x.GameId)
        .Concat(dislikes.Select(x => x.GameId))
        .Concat(favorites.Select(x => x.GameId))
        .Concat(plans.Select(x => x.GameId))
        .Concat(progresses.Select(x => x.GameId))
        .Concat(reviews.Select(x => x.GameId))
        .Concat(ratings.Select(x => x.GameId))
        .Where(g => !string.IsNullOrEmpty(g))
        .Distinct()
        .ToList();

    var games = await _games
        .Find(g => gameIds.Contains(g.Id))
        .Project(g => new
        {
            g.Id,
            Title = g.Game_Name,
            Cover = g.Poster_Image.URL 
        })
        .ToListAsync(ct);

    var gameLookup = games.ToDictionary(g => g.Id, g => g);

    var items = new List<UserActivityItemDto>();

    // --- Like'lar ---
    foreach (var l in likes)
    {
        if (string.IsNullOrEmpty(l.GameId)) continue;
        gameLookup.TryGetValue(l.GameId, out var g);

        items.Add(new UserActivityItemDto
        {
            Id = l.Id,
            Type = "liked",
            GameId = l.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = l.CreatedAt
        });
    }

    // --- Dislike'lar ---
    foreach (var d in dislikes)
    {
        if (string.IsNullOrEmpty(d.GameId)) continue;
        gameLookup.TryGetValue(d.GameId, out var g);

        items.Add(new UserActivityItemDto
        {
            Id = d.Id,
            Type = "disliked",
            GameId = d.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = d.CreatedAt
        });
    }

    // --- Favorite (Loved) ---
    foreach (var f in favorites)
    {
        if (string.IsNullOrEmpty(f.GameId)) continue;
        gameLookup.TryGetValue(f.GameId, out var g);

        items.Add(new UserActivityItemDto
        {
            Id = f.Id,
            Type = "loved",
            GameId = f.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = f.CreatedAt
        });
    }

    // --- Plan to Play ---
    foreach (var p in plans)
    {
        if (string.IsNullOrEmpty(p.GameId)) continue;
        gameLookup.TryGetValue(p.GameId, out var g);

        items.Add(new UserActivityItemDto
        {
            Id = p.Id,
            Type = "plan-to-play",
            GameId = p.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = p.CreatedAt
        });
    }

    // --- Progress ---
    foreach (var pr in progresses)
    {
        if (string.IsNullOrEmpty(pr.GameId)) continue;
        gameLookup.TryGetValue(pr.GameId, out var g);

        items.Add(new UserActivityItemDto
        {
            Id = pr.Id,
            Type = "progress",
            GameId = pr.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = pr.CreatedAt,
            ProgressPercent = pr.Progress   // property ismi farklıysa düzelt
        });
    }

    // --- Reviews ---
    foreach (var r in reviews)
    {
        if (string.IsNullOrEmpty(r.GameId)) continue;
        gameLookup.TryGetValue(r.GameId, out var g);

        items.Add(new UserActivityItemDto
        {
            Id = r.Id,
            Type = "review",
            GameId = r.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = r.TodayDate,
            ReviewText = r.Comment,     // kendi Review modeline göre: r.Content, r.Body vs olabilir
            ReviewRating = r.StarCount   // 0–10 gibi; adını modeline göre düzelt
        });
    }

    // --- GameRatings (rating + alt başlıklar) ---
    foreach (var gr in ratings)
    {
        if (string.IsNullOrEmpty(gr.GameId)) continue;
        gameLookup.TryGetValue(gr.GameId, out var g);

        var subRatings = new List<UserActivitySubRatingDto>
        {
            new() { Label = "Music & Sound",    Value = gr.MusicAndSound,    Max = 10 },
            new() { Label = "Story & Writing",  Value = gr.StoryAndWriting,  Max = 10 },
            new() { Label = "Gameplay",         Value = gr.Gameplay,         Max = 10 },
            new() { Label = "Visuals",          Value = gr.Visuals,          Max = 10 },
            new() { Label = "Bugs & Stability", Value = gr.BugsAndStability, Max = 10 },
            new() { Label = "Replayability",    Value = gr.Replayability,    Max = 10 }
        };

        items.Add(new UserActivityItemDto
        {
            Id = gr.Id.ToString(),
            Type = "rating",
            GameId = gr.GameId,
            GameName = g?.Title ?? "(Unknown Game)",
            GameCover = g?.Cover,
            Timestamp = gr.UpdatedAt,
            OverallRating = gr.OverallScore,
            SubRatings = subRatings
        });
    }

    // --- Hepsini tarihe göre sırala + limit uygula ---
    var ordered = items
        .OrderByDescending(x => x.Timestamp)
        .ToList();

    var limited = ordered.Take(limit).ToList();

    var response = new UserActivityResponseDto
    {
        Items = limited,
        Total = ordered.Count
    };

    return Ok(response);
}



[HttpGet("by-username/{userName}")]
public async Task<ActionResult<UserProfileDto>> GetByUserName(
    string userName,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(userName))
        return BadRequest("Username gerekli.");

    var user = await _users
        .Find(u => u.UserName == userName)
        .FirstOrDefaultAsync(ct);

    if (user == null)
        return NotFound("Kullanıcı bulunamadı.");

    var dto = new UserProfileDto
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        Birthdate = user.Birthdate,
        Country = user.Country,
        City = user.City,
        ProfileImageUrl = user.ProfileImageUrl,
        BannerUrl = user.BannerUrl,
        About = user.About,
        FavoriteGenres = user.FavoriteGenres,
        Platforms = user.Platforms,
        Badge = user.Badge,
        Title = user.Title,
        FavConsoles = user.FavConsoles,
        Equipment = user.Equipment,
        Awards = user.Awards,
        Experiences = user.Experiences,
        Projects = user.Projects,
        Educations = user.Educations,
        Createdat = user.Createdat, // modelde neyse ona göre
        Skills = user.Skills,
        Name = user.Name,
        Surname = user.Surname,
        CareerGoal = user.CareerGoal,
        ContactUrl = user.ContactUrl,

        
    };

    return Ok(dto);
}

[HttpGet("{id:length(24)}/gaming-stats")]
public async Task<ActionResult<UserGamingStatsDto>> GetUserGamingStats(
    string id,
    CancellationToken ct = default)
{
    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        return BadRequest("Geçersiz id.");

    var exists = await _users.Find(u => u.Id == id).AnyAsync(ct);
    if (!exists)
        return NotFound("Kullanıcı bulunamadı.");


    // ------------------------------------------------
    // 1) TÜM KULLANICI OYUN ETKİLEŞİMLERİNİ ÇEK
    // ------------------------------------------------

    var likes = await _gameLikes
        .Find(x => x.UserId == id)
        .ToListAsync(ct);

    var favorites = await _gameFavorites
        .Find(x => x.UserId == id)
        .ToListAsync(ct);

    var reviews = await _reviews
        .Find(x => x.UserId == id)
        .ToListAsync(ct);

    var ratings = await _gameRatings
        .Find(x => x.UserId == id)
        .ToListAsync(ct);

    var progressList = await _gameProgresses
        .Find(x => x.UserId == id)
        .ToListAsync(ct);

    var planToPlay = await _gamePlans
        .Find(x => x.UserId == id)
        .ToListAsync(ct);



    // ------------------------------------------------
    // 2) PROGRESS → COMPLETION RATE hesapla
    // ------------------------------------------------

    var latestProgress = progressList
        .Where(p => !string.IsNullOrEmpty(p.GameId))
        .GroupBy(p => p.GameId!)
        .Select(g =>
        {
            var last = g.OrderByDescending(x => x.CreatedAt).First();
            return last.Progress ?? 0;
        })
        .ToList();

    double completionRate = latestProgress.Any()
        ? Math.Clamp(latestProgress.Average(), 0, 100)
        : 0;



    // ------------------------------------------------
    // 3) HER OYUN İÇİN AĞIRLIKLI SCORE HESAPLA
    // ------------------------------------------------

    // OyunId -> Score
    var gameScores = new Dictionary<string, double>();

    // BASE WEIGHTS
    const double W_LIKE = 2.0;
    const double W_FAVORITE = 4.0;
    const double W_PROGRESS_FINISHED = 2.5;
    const double W_PROGRESS_PLAYING = 1.2;
    const double W_PLAN = 0.3;
    const double W_REVIEW = 0.8; // star * 0.8
    const double W_RATING = 0.5; // rating * 0.5


    // LIKE
    foreach (var l in likes)
    {
        if (!gameScores.ContainsKey(l.GameId)) gameScores[l.GameId] = 0;
        gameScores[l.GameId] += W_LIKE;
    }

    // FAVORITE
    foreach (var fav in favorites)
    {
        if (!gameScores.ContainsKey(fav.GameId)) gameScores[fav.GameId] = 0;
        gameScores[fav.GameId] += W_FAVORITE;
    }

    // PLAN
    foreach (var p in planToPlay)
    {
        if (!gameScores.ContainsKey(p.GameId)) gameScores[p.GameId] = 0;
        gameScores[p.GameId] += W_PLAN;
    }

    // REVIEWS
    foreach (var r in reviews)
    {
        if (!gameScores.ContainsKey(r.GameId)) gameScores[r.GameId] = 0;

        double stars = r.StarCount;  // 0–5
        gameScores[r.GameId] += (stars * W_REVIEW);
    }

    // RATINGS 0–10
    foreach (var gr in ratings)
    {
        if (!gameScores.ContainsKey(gr.GameId)) gameScores[gr.GameId] = 0;

        double rating = gr.OverallScore;  // 0–10
        gameScores[gr.GameId] += (rating * W_RATING);
    }

    // PROGRESS
    foreach (var pr in progressList)
    {
        if (!gameScores.ContainsKey(pr.GameId)) gameScores[pr.GameId] = 0;

        int progress = pr.Progress ?? 0;

        if (progress >= 100) gameScores[pr.GameId] += W_PROGRESS_FINISHED;
        else if (progress > 0) gameScores[pr.GameId] += W_PROGRESS_PLAYING;
    }



    // ------------------------------------------------
    // 4) GENRE PUANLARINI TOPLA
    // ------------------------------------------------

    var gameIds = gameScores.Keys.ToList();

    var details = await _gameDetails
        .Find(d => gameIds.Contains(d.GameId))
        .ToListAsync(ct);

    // GenreId -> Score
    var genreScores = new Dictionary<string, double>();

    foreach (var d in details)
    {
        if (d.GenreIds == null || !gameScores.ContainsKey(d.GameId))
            continue;

        double score = gameScores[d.GameId];
        int genreCount = d.GenreIds.Count;

        if (genreCount == 0) continue;

        double perGenre = score / genreCount;

        foreach (var gid in d.GenreIds)
        {
            if (!genreScores.ContainsKey(gid)) genreScores[gid] = 0;
            genreScores[gid] += perGenre;
        }
    }

    // GENRE ADLARI
    var allGenreIds = genreScores.Keys.ToList();

    var genreModels = await _genres
        .Find(g => allGenreIds.Contains(g.Id))
        .ToListAsync(ct);

    var lookup = genreModels.ToDictionary(g => g.Id, g => g.Name ?? "Unknown");

    double totalGenreScore = genreScores.Values.Sum();

    var finalGenreStats = genreScores
        .Select(kvp => new GenreStatDto
        {
            Name = lookup.TryGetValue(kvp.Key, out var n) ? n : "Unknown",
            Count = 0, // artık önemli değil
            Percent = totalGenreScore > 0
                ? Math.Round(kvp.Value * 100 / totalGenreScore, 1)
                : 0
        })
        .OrderByDescending(g => g.Percent)
        .ToList();


    // ------------------------------------------------
    // 5) RESPONSE
    // ------------------------------------------------

    var dto = new UserGamingStatsDto
    {
        CompletionRate = Math.Round(completionRate, 1),
        TotalTrackedGames = latestProgress.Count,
        Genres = finalGenreStats
    };

    return Ok(dto);
}


[HttpGet("{id:length(24)}/awards")]
public async Task<ActionResult<UserAwardsDto>> GetUserAwards(
    string id,
    CancellationToken ct = default)
{
    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        return BadRequest("Geçersiz id.");

    var exists = await _users.Find(u => u.Id == id).AnyAsync(ct);
    if (!exists)
        return NotFound("Kullanıcı bulunamadı.");

    // 1) Kullanıcının tüm progress kayıtlarını çek
    var progressList = await _gameProgresses
        .Find(p => p.UserId == id && p.Progress != null && p.Progress > 0)
        .ToListAsync(ct);

    // Hiç progress yoksa direkt boş dön
    if (!progressList.Any())
    {
        return Ok(new UserAwardsDto
        {
            GamesPlayedCount = 0,
            CompletedGamesCount = 0,
            CompletionRatePercent = 0,
            ReviewedGamesCount = 0,
            ReviewApprovalRatePercent = 0
        });
    }

    // 2) Her oyun için EN SON progress değerini al
    var lastProgressPerGame = progressList
        .Where(p => !string.IsNullOrEmpty(p.GameId))
        .GroupBy(p => p.GameId!)
        .Select(g =>
        {
            var last = g.OrderByDescending(x => x.CreatedAt).First();
            return new
            {
                GameId = g.Key,
                Progress = last.Progress ?? 0
            };
        })
        .ToList();

    // 3) Games Played = progress >= 30
    var playedGameIds = lastProgressPerGame
        .Where(x => x.Progress >= 30)
        .Select(x => x.GameId)
        .ToList();

    var gamesPlayedCount = playedGameIds.Count;

    // 4) Completed = progress >= 80
    var completedGamesCount = lastProgressPerGame.Count(x => x.Progress >= 80);

    double completionRatePercent = 0;
    if (gamesPlayedCount > 0)
    {
        completionRatePercent = (double)completedGamesCount / gamesPlayedCount * 100.0;
    }

    // 5) Review Approval Rate:
    //    GamesPlayed oyunlarından kaç tanesine review yazılmış?
    var reviewedGameIds = await _reviews
        .Find(r => r.UserId == id && playedGameIds.Contains(r.GameId))
        .Project(r => r.GameId)
        .ToListAsync(ct);

    var reviewedGamesCount = reviewedGameIds
        .Where(gid => !string.IsNullOrEmpty(gid))
        .Distinct()
        .Count();

    double reviewApprovalRatePercent = 0;
    if (gamesPlayedCount > 0)
    {
        reviewApprovalRatePercent = (double)reviewedGamesCount / gamesPlayedCount * 100.0;
    }

    var dto = new UserAwardsDto
    {
        GamesPlayedCount = gamesPlayedCount,
        CompletedGamesCount = completedGamesCount,
        CompletionRatePercent = Math.Round(completionRatePercent, 1),
        ReviewedGamesCount = reviewedGamesCount,
        ReviewApprovalRatePercent = Math.Round(reviewApprovalRatePercent, 1)
    };

    return Ok(dto);
}


[HttpGet("{id:length(24)}/gamification-summary")]
public async Task<ActionResult<UserGamificationSummaryDto>> GetGamificationSummary(
    string id,
    CancellationToken ct = default)
{
    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        return BadRequest("Geçersiz id.");

    var exists = await _users.Find(u => u.Id == id).AnyAsync(ct);
    if (!exists)
        return NotFound("Kullanıcı bulunamadı.");

    var summary = await _gamification.GetUserSummaryAsync(id);
    return Ok(summary);
}


private static string? GetUserIdFromClaims(ClaimsPrincipal user) =>
    user.FindFirst("id")?.Value // token'ında ObjectId burada
    ?? user.FindFirstValue(ClaimTypes.NameIdentifier) // yedek
    ?? user.FindFirstValue("sub");




}
