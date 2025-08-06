using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CommentToGame.Models;
using CommentToGame.Data;
using CommentToGame.Dtos;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;


namespace CommentToGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserDto request)
    {
        if (await _context.Users.AnyAsync(u => u.UserName == request.UserName || u.Email == request.Email))
            return BadRequest("Bu kullanıcı adı veya email zaten var.");

        var user = new User
        {
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok("Kayıt başarılı.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
        if (user == null)
            return BadRequest("Kullanıcı bulunamadı.");

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
            return BadRequest("Şifre yanlış.");

        string token = CreateToken(user);
        string refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);


        await _context.SaveChangesAsync();

        return Ok(new { token, refreshToken });

    }

    
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
        if (user == null || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            return Unauthorized("Geçersiz veya süresi dolmuş refresh token.");

        string newAccessToken = CreateToken(user);
        string newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
       int refreshExpireDays = int.TryParse(_config["Jwt:RefreshTokenExpireDays"], out var rDays) ? rDays : 7;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshExpireDays);


        await _context.SaveChangesAsync();

        return Ok(new { token = newAccessToken, refreshToken = newRefreshToken });
    }




    private string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

     int expireDays = int.TryParse(_config["Jwt:ExpireDays"], out var days) ? days : 1;


        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(expireDays), //Burada token süresini uzattık çıkış yapmadan çıkış yapmıycak token unutulmayacak
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }


}
