using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMongoCollection<User> _users;

    public UsersController(MongoDbService db)
    {
        _users = db.GetCollection<User>("User"); // koleksiyon adın neyse
    }

    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User user, CancellationToken ct)
    {
        user.Createdat = DateTime.UtcNow;
        await _users.InsertOneAsync(user, cancellationToken: ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
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

 
    [HttpPut("{id:length(24)}")]
    public async Task<IActionResult> Update(string id, [FromBody] User updatedUser, CancellationToken ct)
    {
        var existing = await _users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (existing == null) return NotFound();

        updatedUser.Id = id; // id sabit kalmalı
        var result = await _users.ReplaceOneAsync(u => u.Id == id, updatedUser, cancellationToken: ct);

        if (result.ModifiedCount == 0)
            return BadRequest("User update failed.");

        return NoContent();
    }


    [HttpDelete("{id:length(24)}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var result = await _users.DeleteOneAsync(u => u.Id == id, ct);
        if (result.DeletedCount == 0)
            return NotFound();

        return NoContent();
    }
}
