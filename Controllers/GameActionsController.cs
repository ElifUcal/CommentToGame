using CommentToGame.Data;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommentToGame.DTOs;

namespace CommentToGame.Controllers
{
    // Progress update isteği için basit DTO
    public class UpdateProgressDto
    {
        public int Progress { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 🔒 tüm aksiyonlar için auth zorunlu (JWT)
    public class GameActionsController : ControllerBase
    {
        private readonly MongoDbService _db;

        public GameActionsController(MongoDbService db)
        {
            _db = db;
        }

        // ✅ Giriş kontrolü yapan güvenli helper
        private string GetUserId()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                throw new UnauthorizedAccessException("Please log in to perform this action.");

            var id = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (string.IsNullOrEmpty(id))
                throw new UnauthorizedAccessException("Please log in to perform this action.");

            return id;
        }

        // ---------------- LIKE ----------------
        [HttpPost("like/{gameId}")]
        public async Task<IActionResult> LikeGame(string gameId)
        {
            try
            {
                var userId = GetUserId();
                var likes = _db.GetCollection<GameLike>("game_likes");
                var dislikes = _db.GetCollection<GameDislike>("game_dislikes");

                var liked = await likes.Find(x => x.GameId == gameId && x.UserId == userId)
                                       .FirstOrDefaultAsync();

                if (liked != null)
                {
                    await likes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Like removed" });
                }

                await dislikes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                await likes.InsertOneAsync(new GameLike
                {
                    GameId = gameId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });

                return Ok(new { message = "Liked" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ---------------- DISLIKE ----------------
        [HttpPost("dislike/{gameId}")]
        public async Task<IActionResult> DislikeGame(string gameId)
        {
            try
            {
                var userId = GetUserId();
                var dislikes = _db.GetCollection<GameDislike>("game_dislikes");
                var likes = _db.GetCollection<GameLike>("game_likes");

                var disliked = await dislikes.Find(x => x.GameId == gameId && x.UserId == userId)
                                             .FirstOrDefaultAsync();

                if (disliked != null)
                {
                    await dislikes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Dislike removed" });
                }

                await likes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                await dislikes.InsertOneAsync(new GameDislike
                {
                    GameId = gameId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });

                return Ok(new { message = "Disliked" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ---------------- FAVORITE ----------------
        [HttpPost("favorite/{gameId}")]
        public async Task<IActionResult> FavoriteGame(string gameId)
        {
            try
            {
                var userId = GetUserId();
                var favorites = _db.GetCollection<GameFavorite>("game_favorites");

                var fav = await favorites.Find(x => x.GameId == gameId && x.UserId == userId)
                                         .FirstOrDefaultAsync();

                if (fav != null)
                {
                    await favorites.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Removed from favorites" });
                }

                await favorites.InsertOneAsync(new GameFavorite
                {
                    GameId = gameId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });

                return Ok(new { message = "Added to favorites" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ---------------- PLAN TO PLAY (YENİ) ----------------
        // POST api/GameActions/plantoplay/{gameId}
        [HttpPost("plantoplay/{gameId}")]
        public async Task<IActionResult> PlanToPlayGame(string gameId)
        {
            try
            {
                var userId = GetUserId();
                var planCol = _db.GetCollection<GamePlantoPlay>("game_plantoplay");

                var existing = await planCol
                    .Find(x => x.GameId == gameId && x.UserId == userId)
                    .FirstOrDefaultAsync();

                // varsa sil → toggle davranışı
                if (existing != null)
                {
                    await planCol.DeleteOneAsync(x => x.Id == existing.Id);
                    return Ok(new { message = "Removed from plan-to-play list" });
                }

                // yoksa ekle
                var ptp = new GamePlantoPlay
                {
                    GameId = gameId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await planCol.InsertOneAsync(ptp);

                return Ok(new { message = "Added to plan-to-play list" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ---------------- PROGRESS ----------------
        [HttpPost("progress/{gameId}")]
        public async Task<IActionResult> SetGameProgress(string gameId, [FromBody] UpdateProgressDto dto)
        {
            try
            {
                var userId = GetUserId();
                var progressCol = _db.GetCollection<GameProgress>("game_progress");

                // 0-100 validasyon
                if (dto == null)
                    return BadRequest(new { message = "Progress body is required." });

                if (dto.Progress < 0 || dto.Progress > 100)
                    return BadRequest(new { message = "Progress must be between 0 and 100." });

                var existing = await progressCol
                    .Find(x => x.GameId == gameId && x.UserId == userId)
                    .FirstOrDefaultAsync();

                // Progress 0 ise kaydı tamamen sil
                if (dto.Progress == 0)
                {
                    if (existing != null)
                    {
                        await progressCol.DeleteOneAsync(x => x.Id == existing.Id);
                    }

                    return Ok(new
                    {
                        message = "Progress cleared",
                        progress = 0
                    });
                }

                if (existing == null)
                {
                    var gp = new GameProgress
                    {
                        GameId = gameId,
                        UserId = userId,
                        Progress = dto.Progress,
                        CreatedAt = DateTime.UtcNow
                    };

                    await progressCol.InsertOneAsync(gp);
                }
                else
                {
                    var update = Builders<GameProgress>.Update
                        .Set(x => x.Progress, dto.Progress)
                        .Set(x => x.CreatedAt, DateTime.UtcNow);

                    await progressCol.UpdateOneAsync(
                        x => x.Id == existing.Id,
                        update
                    );
                }

                return Ok(new
                {
                    message = "Progress updated",
                    progress = dto.Progress
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ---------------- STATUS ----------------
        [HttpGet("status/{gameId}")]
        public async Task<IActionResult> GetGameActionStatus(string gameId)
        {
            var userId = GetUserId();
            if (userId == "anonymous")
                return Unauthorized("Lütfen giriş yapın.");

            var progressCol = _db.GetCollection<GameProgress>("game_progress");
            var favorites = _db.GetCollection<GameFavorite>("game_favorites");
            var planCol = _db.GetCollection<GamePlantoPlay>("game_plantoplay");
            var dislikes = _db.GetCollection<GameDislike>("game_dislikes");
            var likes = _db.GetCollection<GameLike>("game_likes");

            var like = await likes.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();
            var dislike = await dislikes.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();
            var favorite = await favorites.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();
            var planToPlay = await planCol.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();

            var progressDoc = await progressCol
                .Find(x => x.GameId == gameId && x.UserId == userId)
                .FirstOrDefaultAsync();

            var hasProgress = progressDoc != null;
            int? progressValue = progressDoc?.Progress;

            return Ok(new
            {
                like,
                dislike,
                favorite,
                planToPlay,
                save = hasProgress,
                progress = progressValue ?? 0
            });
        }

        // ---------------- RECENT ACTIONS ----------------
        
[HttpGet("recent")]
public async Task<IActionResult> GetRecentGameActions(
    [FromQuery] int limit = 4,
    [FromQuery] bool excludeProgress = false,
    [FromQuery] string? userId = null          // 🔥 yeni param
)
{
    // 1) Hedef kullanıcıyı belirle
    string targetUserId;

    if (!string.IsNullOrWhiteSpace(userId))
    {
        // Profil için özel istek geliyor (başkasının profili)
        targetUserId = userId;
    }
    else
    {
        // Query'de userId yoksa → current user
        try
        {
            targetUserId = GetUserId();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // ObjectId kontrolü
    if (!MongoDB.Bson.ObjectId.TryParse(targetUserId, out _))
        return BadRequest("Geçersiz userId.");

    if (limit <= 0) limit = 4;
    if (limit > 50) limit = 50;

    var likesCol     = _db.GetCollection<GameLike>("game_likes");
    var dislikesCol  = _db.GetCollection<GameDislike>("game_dislikes");
    var favoritesCol = _db.GetCollection<GameFavorite>("game_favorites");
    var planCol      = _db.GetCollection<GamePlantoPlay>("game_plantoplay");
    var progressCol  = _db.GetCollection<GameProgress>("game_progress");

    var gamesCol   = _db.GetCollection<Game>("Games");
    var detailsCol = _db.GetCollection<Game_Details>("GameDetails");
    var reviewsCol = _db.GetCollection<Reviews>("reviews");

    // 🔥 Buradan itibaren her yerde userId yerine targetUserId kullanıyoruz
    var likesTask = likesCol
        .Find(x => x.UserId == targetUserId)
        .SortByDescending(x => x.CreatedAt)
        .Limit(limit)
        .ToListAsync();

    var dislikesTask = dislikesCol
        .Find(x => x.UserId == targetUserId)
        .SortByDescending(x => x.CreatedAt)
        .Limit(limit)
        .ToListAsync();

    var favoritesTask = favoritesCol
        .Find(x => x.UserId == targetUserId)
        .SortByDescending(x => x.CreatedAt)
        .Limit(limit)
        .ToListAsync();

    var planTask = planCol
        .Find(x => x.UserId == targetUserId)
        .SortByDescending(x => x.CreatedAt)
        .Limit(limit)
        .ToListAsync();

    Task<List<GameProgress>> progressTask;
    if (excludeProgress)
    {
        progressTask = Task.FromResult(new List<GameProgress>());
    }
    else
    {
        progressTask = progressCol
            .Find(x => x.UserId == targetUserId && x.Progress != null)
            .SortByDescending(x => x.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    await Task.WhenAll(likesTask, dislikesTask, favoritesTask, planTask, progressTask);

    var actions = new List<RecentGameActionDto>();

    actions.AddRange(likesTask.Result.Select(x => new RecentGameActionDto
    {
        GameId = x.GameId,
        ActionType = "like",
        CreatedAt = x.CreatedAt
    }));

    actions.AddRange(dislikesTask.Result.Select(x => new RecentGameActionDto
    {
        GameId = x.GameId,
        ActionType = "dislike",
        CreatedAt = x.CreatedAt
    }));

    actions.AddRange(favoritesTask.Result.Select(x => new RecentGameActionDto
    {
        GameId = x.GameId,
        ActionType = "favorite",
        CreatedAt = x.CreatedAt
    }));

    actions.AddRange(planTask.Result.Select(x => new RecentGameActionDto
    {
        GameId = x.GameId,
        ActionType = "plan_to_play",
        CreatedAt = x.CreatedAt
    }));

    if (!excludeProgress)
    {
        actions.AddRange(progressTask.Result.Select(x => new RecentGameActionDto
        {
            GameId = x.GameId,
            ActionType = "progress",
            CreatedAt = x.CreatedAt,
            ProgressPercent = x.Progress
        }));
    }

    actions = actions
        .OrderByDescending(a => a.CreatedAt)
        .Take(limit)
        .ToList();

    var gameIds = actions.Select(a => a.GameId).Distinct().ToList();

    var games = await gamesCol
        .Find(g => gameIds.Contains(g.Id))
        .ToListAsync();
    var gamesDict = games.ToDictionary(g => g.Id, g => g);

    var details = await detailsCol
        .Find(d => gameIds.Contains(d.GameId))
        .ToListAsync();
    var detailsDict = details
        .GroupBy(d => d.GameId)
        .ToDictionary(g => g.Key, g => g.First());

    var reviews = await reviewsCol
        .Find(r => gameIds.Contains(r.GameId))
        .ToListAsync();

    var avgRatings = reviews
        .Where(r => r.StarCount != null)
        .GroupBy(r => r.GameId)
        .ToDictionary(
            g => g.Key,
            g => g.Average(r => r.StarCount)
        );

    foreach (var a in actions)
    {
        gamesDict.TryGetValue(a.GameId, out var g);
        detailsDict.TryGetValue(a.GameId, out var gd);

        a.Title       = g?.Game_Name;
        a.Developer   = gd?.Developer ?? g?.Studio;
        a.ReleaseYear = g?.Release_Date?.Year;
        a.MatchScore  = g?.GgDb_Rating ?? g?.Metacritic_Rating ?? 0;

        if (avgRatings.TryGetValue(a.GameId, out var avg))
        {
            a.StarRating = Math.Round(avg, 1);
        }

        a.ImageUrl = g?.Main_image_URL;
    }

    return Ok(actions);
}


 public class GamingExperienceSummaryDto
    {
        public int GamesPlayed { get; set; }
        public int LikedGames { get; set; }
        public int DislikedGames { get; set; }
        public int PlanToPlay { get; set; }
    }

// ---------------- SUMMARY (Sade Sayılar) ----------------
[HttpGet("summary")]
public async Task<IActionResult> GetGamingExperienceSummary([FromQuery] string? userId = null)
{
    string targetUserId;

    if (!string.IsNullOrWhiteSpace(userId))
    {
        // Profil için gelen userId
        targetUserId = userId;
    }
    else
    {
        // Login olan kullanıcı
        try
        {
            targetUserId = GetUserId();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    var progressCol = _db.GetCollection<GameProgress>("game_progress");
    var likesCol    = _db.GetCollection<GameLike>("game_likes");
    var dislikesCol = _db.GetCollection<GameDislike>("game_dislikes");
    var planCol     = _db.GetCollection<GamePlantoPlay>("game_plantoplay");

    var progressDocs = await progressCol
        .Find(x => x.UserId == targetUserId)
        .ToListAsync();

    int gamesPlayed = progressDocs
        .Select(x => x.GameId)
        .Distinct()
        .Count();

    int likedGames = (int)await likesCol.CountDocumentsAsync(x => x.UserId == targetUserId);
    int dislikedGames = (int)await dislikesCol.CountDocumentsAsync(x => x.UserId == targetUserId);
    int planToPlay = (int)await planCol.CountDocumentsAsync(x => x.UserId == targetUserId);

    var dto = new GamingExperienceSummaryDto
    {
        GamesPlayed = gamesPlayed,
        LikedGames = likedGames,
        DislikedGames = dislikedGames,
        PlanToPlay = planToPlay
    };

    return Ok(dto);
}



    }
}
