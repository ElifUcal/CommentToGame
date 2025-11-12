// Controllers/SettingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // RBAC
public class SettingsController : ControllerBase
{
    private readonly SettingsRepository _repo;
    private readonly AppSettingsValidator _validator = new();

    public SettingsController(SettingsRepository repo) { _repo = repo; }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _repo.GetAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] AppSettings dto, CancellationToken ct)
    {
        var v = _validator.Validate(dto);
        if (!v.IsValid) return BadRequest(v.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var user = User.Identity?.Name ?? "system";
        var (ok, saved, err) = await _repo.UpdateAsync(dto, user, ct);
        if (!ok && err == "VERSION_CONFLICT") return StatusCode(409, new { message = "Version conflict", server = saved });
        return Ok(saved);
    }

    // Binary upload'u ayır: URL döndür (S3/Cloudinary vs.)
    [HttpPost("favicon")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> UploadFavicon([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");
        if (!new[] { "image/x-icon", "image/png" }.Contains(file.ContentType)) return BadRequest("Only .ico or .png");
        // örnek: local kaydetme (prod’da S3, Cloudinary kullan)
        var name = $"favicon_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine("wwwroot", "uploads", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs, ct);
        var url = $"/uploads/{name}";
        return Ok(new { url });
    }
}
