using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMongoCollection<User>? _users;

    public UsersController(MongoDbService db)
    {
        _users = db.GetCollection<User>("User"); // koleksiyon adın neyse
    }

    // GET /api/users/{id}
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
    
    // GET /api/users/basic?ids=1,2,3
[HttpGet("basic")]
public async Task<IActionResult> GetBasic([FromQuery] string ids, CancellationToken ct)
{
    var arr = (ids ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct()
        .ToList();
    if (arr.Count == 0) return Ok(new { users = Array.Empty<object>() });

    var users = await _users.Find(u => arr.Contains(u.Id))
        .Project(u => new {
            id = u.Id,
            userName = u.UserName,
            profileImageUrl = u.ProfileImageUrl
        })
        .ToListAsync(ct);

    return Ok(new { users });
}

}
