using CommentToGame.Data;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.Controllers
{
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

                var liked = await likes.Find(x => x.GameId == gameId && x.UserId == userId).FirstOrDefaultAsync();

                if (liked != null)
                {
                    await likes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Like removed" });
                }

                await dislikes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                await likes.InsertOneAsync(new GameLike { GameId = gameId, UserId = userId });
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

                var disliked = await dislikes.Find(x => x.GameId == gameId && x.UserId == userId).FirstOrDefaultAsync();

                if (disliked != null)
                {
                    await dislikes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Dislike removed" });
                }

                await likes.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                await dislikes.InsertOneAsync(new GameDislike { GameId = gameId, UserId = userId });
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

                var fav = await favorites.Find(x => x.GameId == gameId && x.UserId == userId).FirstOrDefaultAsync();

                if (fav != null)
                {
                    await favorites.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Removed from favorites" });
                }

                await favorites.InsertOneAsync(new GameFavorite { GameId = gameId, UserId = userId });
                return Ok(new { message = "Added to favorites" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        // ---------------- SAVE ----------------
        [HttpPost("save/{gameId}")]
        public async Task<IActionResult> SaveGame(string gameId)
        {
            try
            {
                var userId = GetUserId();
                var saves = _db.GetCollection<GameSave>("game_saves");

                var saved = await saves.Find(x => x.GameId == gameId && x.UserId == userId).FirstOrDefaultAsync();

                if (saved != null)
                {
                    await saves.DeleteOneAsync(x => x.GameId == gameId && x.UserId == userId);
                    return Ok(new { message = "Unsaved" });
                }

                await saves.InsertOneAsync(new GameSave { GameId = gameId, UserId = userId });
                return Ok(new { message = "Saved" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }


        [HttpGet("status/{gameId}")]
public async Task<IActionResult> GetGameActionStatus(string gameId)
{
    var userId = GetUserId();
            if (userId == "anonymous")
                return Unauthorized("Lütfen giriş yapın.");

            var saves = _db.GetCollection<GameSave>("game_saves");
            var favorites = _db.GetCollection<GameFavorite>("game_favorites");
            var dislikes = _db.GetCollection<GameDislike>("game_dislikes");
            var likes = _db.GetCollection<GameLike>("game_likes");



    var like = await likes.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();
    var dislike = await dislikes.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();
    var favorite = await favorites.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();
    var save = await saves.Find(x => x.GameId == gameId && x.UserId == userId).AnyAsync();

    return Ok(new
    {
        like,
        dislike,
        favorite,
        save
    });
}



    }


   
}
