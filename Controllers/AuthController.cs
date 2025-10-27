using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authorization;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IConfiguration _config;

    public AuthController(MongoDbService service, IConfiguration config)
    {
        var db = service?.Database
                 ?? throw new InvalidOperationException("MongoDbService.database is null.");

        var usersCollectionName = config["MongoDb:UsersCollection"] ?? "User";

        _users = db.GetCollection<User>(usersCollectionName);
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserDto request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName)
                            || string.IsNullOrWhiteSpace(request.Email)
                            || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Kullanıcı adı, email ve şifre zorunludur.");

        var userName = request.UserName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();  // <— ÖNEMLİ

        // Aynı kullanıcı adı veya email var mı?
        var exists = await _users
            .Find(u => u.UserName == userName || u.Email == email)
            .AnyAsync();

        if (exists)
            return BadRequest("Bu kullanıcı adı veya email zaten var.");

        var user = new User
        {
            UserName = userName,
            Email = email,                                 // <— normalize kaydet
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Birthdate = request.Birthdate,
            Country = request.Country.Trim(),
            Createdat = DateTime.Now,
            isBanned = false,
        };

        await _users.InsertOneAsync(user);
        return Ok("Kayıt başarılı.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Kullanıcı adı ve şifre zorunludur.");
        

        var user = await _users.Find(u => u.UserName == request.UserName).FirstOrDefaultAsync();
        if (user is null)
            return BadRequest("Kullanıcı bulunamadı.");

        if(user.isBanned == true)
        {
            return BadRequest("Hesabınız askıya alınmıştır.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return BadRequest("Şifre yanlış.");

        var token = CreateToken(user);
        var refresh = GenerateRefreshToken();

        int rDays = int.TryParse(_config["Jwt:RefreshTokenExpireDays"], out var rr) ? rr : 7;

        var update = Builders<User>.Update
            .Set(u => u.RefreshToken, refresh)
            .Set(u => u.RefreshTokenExpiryTime, DateTime.UtcNow.AddDays(rDays));

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        return Ok(new { token, refreshToken = refresh });
    }

    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { available = false, message = "Email gerekli." });

        var norm = email.Trim().ToLowerInvariant();

        // Emaili normalize ederek tuttugumuzu düşün doğrudan eşitlik sorgusu yapıyoruz
        var exists = await _users.Find(u => u.Email == norm).AnyAsync();

        return Ok(new { available = !exists });
    }


    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Refresh token gerekli.");

        var user = await _users.Find(u => u.RefreshToken == request.RefreshToken).FirstOrDefaultAsync();
        if (user is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return Unauthorized("Geçersiz veya süresi dolmuş refresh token.");

        var newAccess = CreateToken(user);
        var newRefresh = GenerateRefreshToken();

        int rDays = int.TryParse(_config["Jwt:RefreshTokenExpireDays"], out var rr) ? rr : 7;

        var update = Builders<User>.Update
            .Set(u => u.RefreshToken, newRefresh)
            .Set(u => u.RefreshTokenExpiryTime, DateTime.UtcNow.AddDays(rDays));

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);

        return Ok(new { token = newAccess, refreshToken = newRefresh });
    }

    private string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            new Claim(ClaimTypes.Name,  user.UserName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, user.UserType.ToString())
        };

        var keyStr = _config.GetValue<string>("Jwt:Key")
                     ?? throw new InvalidOperationException("Jwt:Key missing.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        int days = int.TryParse(_config["Jwt:ExpireDays"], out var d) ? d : 1;

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(days),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
    
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Unauthorized();

            var user = await _users.Find(u => u.UserName == userName).FirstOrDefaultAsync();
            if (user == null)
                return NotFound();

            return Ok(new
            {
                userName = user.UserName,
                email = user.Email,
                role = user.UserType.ToString(),
                userType = (int)user.UserType,
                country = user.Country,
                birthdate = user.Birthdate,
                profileImageUrl = user.ProfileImageUrl
                
            });
        }

}
