using System.Security.Claims;
using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.Models;
using Microsoft.AspNetCore.Authorization;
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
    public async Task<IActionResult> Patch(string id, [FromBody] UpdateUserDto dto, CancellationToken ct)
    {
        var requesterId = GetUserIdFromClaims(User);
    var isAdmin = User.IsInRole("Admin");

    if (string.IsNullOrEmpty(requesterId))
        return Unauthorized();

    if (!isAdmin && requesterId != id)
        return Forbid();

        var existing = await _users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);
        if (existing == null) return NotFound();

        var b = Builders<User>.Update;
        var updates = new List<UpdateDefinition<User>>();

        // --- İzin verilen alanlar ---
        if (dto.UserName != null) updates.Add(b.Set(x => x.UserName, dto.UserName));
        if (dto.Email != null) updates.Add(b.Set(x => x.Email, dto.Email));
        if (dto.Birthdate.HasValue) updates.Add(b.Set(x => x.Birthdate, dto.Birthdate.Value));
        if (dto.Country != null) updates.Add(b.Set(x => x.Country, dto.Country));
        if (dto.City != null) updates.Add(b.Set(x => x.City, dto.City));
        if (dto.ProfileImageUrl != null) updates.Add(b.Set(x => x.ProfileImageUrl, dto.ProfileImageUrl));
        if (dto.BannerUrl != null) updates.Add(b.Set(x => x.BannerUrl, dto.BannerUrl));
        if (dto.About != null) updates.Add(b.Set(x => x.About, dto.About));
        if (dto.FavoriteGenres != null) updates.Add(b.Set(x => x.FavoriteGenres, dto.FavoriteGenres));

        if (dto.Platforms != null) updates.Add(b.Set(x => x.Platforms, dto.Platforms));
        if (dto.Badge != null) updates.Add(b.Set(x => x.Badge, dto.Badge));
        if (dto.Title != null) updates.Add(b.Set(x => x.Title, dto.Title));
        if (dto.ContactUrl != null) updates.Add(b.Set(x => x.ContactUrl, dto.ContactUrl));
        if (dto.Skills != null) updates.Add(b.Set(x => x.Skills, dto.Skills));

        if (dto.Experiences != null) updates.Add(b.Set(x => x.Experiences, dto.Experiences));
        if (dto.Projects != null) updates.Add(b.Set(x => x.Projects, dto.Projects));
        if (dto.Educations != null) updates.Add(b.Set(x => x.Educations, dto.Educations));
        if (dto.Awards != null) updates.Add(b.Set(x => x.Awards, dto.Awards));

        // Sadece Admin değiştirebilir


        // --- Korumalı alanlara BİLEREK dokunmuyoruz ---
        // x => x.Id
        // x => x.PasswordHash
        // x => x.RefreshToken
        // x => x.RefreshTokenExpiryTime
        // x => x.UserType
        // x => x.Createdat

        if (updates.Count == 0)
            return BadRequest("Güncellenecek bir alan gönderilmedi.");

        var res = await _users.UpdateOneAsync(x => x.Id == id, b.Combine(updates), cancellationToken: ct);
        if (res.MatchedCount == 0) return NotFound();

        return NoContent();
    }


private static string? GetUserIdFromClaims(ClaimsPrincipal user) =>
    user.FindFirst("id")?.Value // token'ında ObjectId burada
    ?? user.FindFirstValue(ClaimTypes.NameIdentifier) // yedek
    ?? user.FindFirstValue("sub");




}
