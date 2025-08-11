using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CommentToGame.Data;
using CommentToGame.Dtos;
using CommentToGame.Models;
using Microsoft.IdentityModel.Tokens;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;

    public AuthController(IUserRepository users, IConfiguration config)
    {
        _users = users;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserDto request)
    {
        if (await _users.ExistsByUserNameOrEmail(request.UserName, request.Email))
            return BadRequest("Bu kullanıcı adı veya email zaten var.");

        var user = new User
        {
            UserName = request.UserName,
            Email    = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        await _users.Create(user);
        return Ok("Kayıt başarılı.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        var user = await _users.GetByUserName(request.UserName);
        if (user == null) return BadRequest("Kullanıcı bulunamadı.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return BadRequest("Şifre yanlış.");

        var token   = CreateToken(user);
        var refresh = GenerateRefreshToken();

        int rDays = int.TryParse(_config["Jwt:RefreshTokenExpireDays"], out var rr) ? rr : 7;
        user.RefreshToken = refresh;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(rDays);

        await _users.Update(user);
        return Ok(new { token, refreshToken = refresh });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var user = await _users.GetByRefreshToken(request.RefreshToken);
        if (user is null || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            return Unauthorized("Geçersiz veya süresi dolmuş refresh token.");

        var newAccess  = CreateToken(user);
        var newRefresh = GenerateRefreshToken();

        int rDays = int.TryParse(_config["Jwt:RefreshTokenExpireDays"], out var rr) ? rr : 7;
        user.RefreshToken = newRefresh;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(rDays);

        await _users.Update(user);
        return Ok(new { token = newAccess, refreshToken = newRefresh });
    }

    private string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(ClaimTypes.Name,  user.UserName),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),

                // 🔥 Rol claim’i:
                new Claim(ClaimTypes.Role, user.UserType.ToString())
            };

            var keyStr = _config.GetValue<string>("Jwt:Key")
                         ?? throw new InvalidOperationException("Jwt:Key missing.");
            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            int days = int.TryParse(_config["Jwt:ExpireDays"], out var d) ? d : 1;

            var token = new JwtSecurityToken(
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires:   DateTime.UtcNow.AddDays(days),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    private string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
