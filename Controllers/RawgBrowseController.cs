using System;
using System.Linq;
using System.Threading.Tasks;
using CommentToGame.DTOs;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentToGame.Controllers
{
    [ApiController]
    [Route("api/rawg")]
    public sealed class RawgBrowseController : ControllerBase
    {
        private readonly IRawgClient _rawg;
        public RawgBrowseController(IRawgClient rawg) => _rawg = rawg;

        // GET /api/rawg/search?q=witcher&page=1&pageSize=20
        // Sadece { id, name } döner. DBye yazmaz.
    // Controllers/RawgController.cs
// Controllers/RawgController.cs  (senin Search action'ının içi)
[HttpGet("search")]
public async Task<IActionResult> Search(
    [FromQuery] string q,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    if (string.IsNullOrWhiteSpace(q)) return BadRequest("q gerekli");
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 50);

    var data = await _rawg.SearchGamesAsync(q, page, pageSize);

    int? ParseYear(string? s)
        => DateTime.TryParse(s, out var dt) ? dt.Year : (int?)null;

    var items = data.Results.Select(x => new
    {
        id   = x.Id,
        name = x.Name,
        year = ParseYear(x.Released),
        slug = x.Slug,
        // platform adları listesi
        platforms = (x.Platforms ?? new List<RawgPlatformWrapper>())
                    .Select(p => p.Platform?.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList()
    });

    var next = string.IsNullOrEmpty(data.Next) ? null : data.Next;
    return Ok(new { items, next });
}


    }
}
