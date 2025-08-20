using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CommentToGame.Services;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/import/rawg")]
public class RawgImportController : ControllerBase
{
    private readonly RawgImportService _svc;
    private readonly IRawgClient _rawg; // <-- eklendi

    public RawgImportController(RawgImportService svc, IRawgClient rawg) // <-- eklendi
    {
        _svc = svc;
        _rawg = rawg;
    }

    // Arama ile import (ör: gta)
    [HttpPost("search")]
    public async Task<IActionResult> ImportBySearch([FromQuery] string q, [FromQuery] int pageSize = 40, [FromQuery] int maxPages = 5)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "q boş olamaz." });

        var imported = await _svc.ImportAllBySearchAsync(q, pageSize, maxPages);
        return Ok(new { query = q, imported, pageSize, maxPages });
    }

    // Sağlık kontrolü
    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken ct = default)
    {
        var mini = await _svc.TestFetchAsync(ct);
        return Ok(new { ok = true, sampleCount = mini });
    }

    [HttpGet("games/with-details")]
    public async Task<IActionResult> GetGamesWithDetails(
        [FromQuery] string? name,
        [FromQuery] bool officialOnly = false,
        [FromQuery] int take = 50)
    {
        var q = string.IsNullOrWhiteSpace(name) ? "" : name;
        var list = await _svc.SearchGamesWithDetailsAsync(q, officialOnly, take);
        return Ok(list);
    }

    // 👇 YENİ: RAWG id ile tek oyun import
    // Örnek: POST /api/import/rawg/3498
    // RawgImportController.cs
[HttpPost("id/{id:int}")]
public async Task<IActionResult> ImportOneById([FromRoute] int id, CancellationToken ct = default)
{
    var ok = await _svc.ImportOneByIdAsync(id, ct);
    if (!ok) return NotFound(new { message = "RAWG game not found" });
    return Ok(new { ok = true, id });
}

}
