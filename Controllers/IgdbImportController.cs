using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CommentToGame.Services;
using CommentToGame.DTOs; 

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/import/igdb")]
public class IgdbImportController : ControllerBase
{
    private readonly IgdbImportService _svc;

    public IgdbImportController(IgdbImportService svc)
    {
        _svc = svc;
    }

    // Arama ile import (ör: gta) – IGDB'den çekip DB'ye ekler
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
}