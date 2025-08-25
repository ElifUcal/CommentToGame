using System.Data.Common;
using CommentToGame.Data;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IMongoCollection<User> _users;

    private readonly IMongoCollection<Game> _games;
    private readonly IConfiguration _config;

    public AdminController(MongoDbService service, IConfiguration config)
    {
        var db = service?.Database
                ?? throw new InvalidOperationException("MongoDbService.database is null.");

        var usersCollectionName = config["MongoDb:UsersCollection"] ?? "User";

        _games = db.GetCollection<Game>("Games");
        _users = db.GetCollection<User>(usersCollectionName);
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin")] // sadece Admin
    public IActionResult Dashboard()
    {
        return Ok("Admin panel verisi 🔐");
    }

    [HttpGet("usergamecount")]
    [Authorize]
    public async Task<IActionResult> UserGameCount()
    {
        var totalUsers = await _users.CountDocumentsAsync(FilterDefinition<User>.Empty);
        var totalGames = await _games.CountDocumentsAsync(FilterDefinition<Game>.Empty);

        return Ok(new { totalUsers, totalGames });
    }



}
