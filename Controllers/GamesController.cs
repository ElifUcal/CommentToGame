using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.Data;
using CommentToGame.Models;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CommentToGame.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IMongoCollection<Game> _games;
        private readonly IMongoCollection<Game_Details> _gameDetails;
        private readonly IConfiguration _config;
        private readonly ISystemLogger _logger;

        public GamesController(MongoDbService service, IConfiguration config, ISystemLogger logger)
        {
            var db = service?.Database ?? throw new InvalidOperationException("MongoDbService.database is null.");

            _games = db.GetCollection<Game>("Games");
            _gameDetails = db.GetCollection<Game_Details>("GameDetails");

            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("upcominggames")]
        public async Task<IActionResult> GetUpcomingGames(CancellationToken ct = default)
        {
            var upcomingGames = await _games
                .Find(x => x.isUpcoming == true)
                .ToListAsync(ct);

            if (!upcomingGames.Any())
                return NotFound(new { message = "No upcoming games found." });

            // Game Id listesi
            var gameIds = upcomingGames.Select(g => g.Id).ToList();

            // Detayları çek
            var details = await _gameDetails
                .Find(d => gameIds.Contains(d.GameId))
                .ToListAsync(ct);

            // 🔹 Map Game + Game_Details → DTO
            var dtoList = upcomingGames.Select(g =>
            {
                var detail = details.FirstOrDefault(d => d.GameId == g.Id);

                return new
                {
                    id = g.Id,
                    title = g.Game_Name,
                    developer = detail?.Developer ?? g.Studio ?? "Unknown",
                    publisher = detail?.Publisher ?? "Unknown",
                    releaseDate = g.Release_Date?.Year ?? 0,
                    matchScore = g.GgDb_Rating ?? 0,
                    coverUrl = g.Main_image_URL ?? "/images/no-cover.png"
                };
            });


            return Ok(dtoList);
        }
    }
}
