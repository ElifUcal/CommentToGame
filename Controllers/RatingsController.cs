using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CommentToGame.Controllers
{
    [ApiController]
    [Route("api/games/{gameId}/ratings")]
    public class RatingsController : ControllerBase
    {
        private readonly IMongoCollection<GameRating> _ratings;

        public RatingsController(MongoDbService mongoDb)
        {
            var db = mongoDb.Database ?? throw new InvalidOperationException("Mongo database is null.");
            _ratings = db.GetCollection<GameRating>("GameRatings");
        }

        // -------------------------------------------------------------
        // Helper: JWT içinden UserId çek (User.Id = Mongo ObjectId string’i)
        // -------------------------------------------------------------
        private string GetUserId()
        {
            // Token oluştururken eklediğin "id" claim’i (User.Id) öncelikli olsun
            var userId =
                User.FindFirst("id")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("User id claim not found.");

            return userId;
        }

        // -------------------------------------------------------------
        // 1) Kullanıcı oyuna rating ekler / günceller (UPSERT)
        // POST /api/games/{gameId}/ratings
        // -------------------------------------------------------------
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<GameRatingDto>> UpsertRating(
            string gameId,
            [FromBody] UpsertGameRatingRequest request)
        {
            var userId = GetUserId(); // 🔥 Artık burada gerçek User.Id geliyor

            // 0–10 aralığı validasyonu
            double[] scores =
            {
                request.MusicAndSound,
                request.StoryAndWriting,
                request.Gameplay,
                request.Visuals,
                request.BugsAndStability,
                request.Replayability
            };

            if (scores.Any(s => s < 0 || s > 10))
                return BadRequest("All rating values must be between 0 and 10.");

            var now = DateTime.UtcNow;

            // Aynı oyun + aynı user için tek kayıt
            var filter = Builders<GameRating>.Filter.Eq(x => x.GameId, gameId) &
                         Builders<GameRating>.Filter.Eq(x => x.UserId, userId);

            var existing = await _ratings.Find(filter).FirstOrDefaultAsync();

            GameRating rating;

            if (existing == null)
            {
                rating = new GameRating
                {
                    Id        = ObjectId.GenerateNewId(), // önemli
                    GameId    = gameId,
                    UserId    = userId,                    // 🔥 id olarak kaydediyoruz
                    CreatedAt = now
                };
            }
            else
            {
                rating = existing;
            }

            rating.MusicAndSound    = request.MusicAndSound;
            rating.StoryAndWriting  = request.StoryAndWriting;
            rating.Gameplay         = request.Gameplay;
            rating.Visuals          = request.Visuals;
            rating.BugsAndStability = request.BugsAndStability;
            rating.Replayability    = request.Replayability;
            rating.UpdatedAt        = now;
            rating.OverallScore = Math.Round(scores.Average(), 1);


            await _ratings.ReplaceOneAsync(
                filter,
                rating,
                new ReplaceOptions { IsUpsert = true }
            );

            var dto = new GameRatingDto
            {
                GameId           = rating.GameId,
                UserId           = rating.UserId,
                MusicAndSound    = rating.MusicAndSound,
                StoryAndWriting  = rating.StoryAndWriting,
                Gameplay         = rating.Gameplay,
                Visuals          = rating.Visuals,
                BugsAndStability = rating.BugsAndStability,
                Replayability    = rating.Replayability,
                OverallScore     = rating.OverallScore,
                UpdatedAt        = rating.UpdatedAt
            };

            return Ok(dto);
        }

        // -------------------------------------------------------------
        // 2) Kullanıcının kendi rating'ini getir
        // GET /api/games/{gameId}/ratings/me
        // -------------------------------------------------------------
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<GameRatingDto?>> GetMyRating(string gameId)
        {
            var userId = GetUserId();

            var filter = Builders<GameRating>.Filter.Eq(x => x.GameId, gameId) &
                         Builders<GameRating>.Filter.Eq(x => x.UserId, userId);

            var rating = await _ratings.Find(filter).FirstOrDefaultAsync();

            if (rating == null)
                return Ok(null);

            var dto = new GameRatingDto
            {
                GameId           = rating.GameId,
                UserId           = rating.UserId,
                MusicAndSound    = rating.MusicAndSound,
                StoryAndWriting  = rating.StoryAndWriting,
                Gameplay         = rating.Gameplay,
                Visuals          = rating.Visuals,
                BugsAndStability = rating.BugsAndStability,
                Replayability    = rating.Replayability,
                OverallScore     = rating.OverallScore,
                UpdatedAt        = rating.UpdatedAt
            };

            return Ok(dto);
        }

        // -------------------------------------------------------------
        // 3) Oyunun rating özetini getir (ortalama değerler)
        // GET /api/games/{gameId}/ratings/summary
        // -------------------------------------------------------------
        [HttpGet("summary")]
        [AllowAnonymous]
        public async Task<ActionResult<GameRatingSummaryDto>> GetSummary(string gameId)
        {
            var match = Builders<GameRating>.Filter.Eq(x => x.GameId, gameId);

            var aggResult = await _ratings.Aggregate()
                .Match(match)
                .Group(
                    g => g.GameId,
                    grp => new
                    {
                        GameId              = grp.Key,
                        TotalRaters         = grp.Count(),
                        AvgMusicAndSound    = grp.Average(x => x.MusicAndSound),
                        AvgStoryAndWriting  = grp.Average(x => x.StoryAndWriting),
                        AvgGameplay         = grp.Average(x => x.Gameplay),
                        AvgVisuals          = grp.Average(x => x.Visuals),
                        AvgBugsAndStability = grp.Average(x => x.BugsAndStability),
                        AvgReplayability    = grp.Average(x => x.Replayability),
                        AvgOverallScore     = grp.Average(x => x.OverallScore)
                    })
                .FirstOrDefaultAsync();

            if (aggResult == null)
            {
                return Ok(new GameRatingSummaryDto
                {
                    GameId              = gameId,
                    TotalRaters         = 0,
                    AvgMusicAndSound    = 0,
                    AvgStoryAndWriting  = 0,
                    AvgGameplay         = 0,
                    AvgVisuals          = 0,
                    AvgBugsAndStability = 0,
                    AvgReplayability    = 0,
                    AvgOverallScore     = 0
                });
            }

            var dto = new GameRatingSummaryDto
            {
                GameId              = aggResult.GameId,
                TotalRaters         = aggResult.TotalRaters,
                AvgMusicAndSound    = aggResult.AvgMusicAndSound,
                AvgStoryAndWriting  = aggResult.AvgStoryAndWriting,
                AvgGameplay         = aggResult.AvgGameplay,
                AvgVisuals          = aggResult.AvgVisuals,
                AvgBugsAndStability = aggResult.AvgBugsAndStability,
                AvgReplayability    = aggResult.AvgReplayability,
                AvgOverallScore     = Math.Round(aggResult.AvgOverallScore, 1)
            };

            return Ok(dto);
        }

        // -------------------------------------------------------------
        // 4) Kendi rating'ini sil
        // DELETE /api/games/{gameId}/ratings/me
        // -------------------------------------------------------------
        [HttpDelete("me")]
        [Authorize]
        public async Task<IActionResult> DeleteMyRating(string gameId)
        {
            var userId = GetUserId();

            var filter = Builders<GameRating>.Filter.Eq(x => x.GameId, gameId) &
                         Builders<GameRating>.Filter.Eq(x => x.UserId, userId);

            var result = await _ratings.DeleteOneAsync(filter);

            if (result.DeletedCount == 0)
                return NotFound();

            return NoContent();
        }
    }
}
