using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CommentToGame.Services;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/import/igdb")]
public class IgdbImportController : ControllerBase
{
    private readonly IgdbImportService _svc;
    private readonly IIgdbClient _igdb; // <-- eklendi

    public IgdbImportController(IgdbImportService svc, IIgdbClient igdb) // <-- eklendi
    {
        _svc = svc;
        _igdb = igdb;
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

    // 👇 YENİ: IGDB id ile tek oyun import
    // POST /api/import/igdb/239064
    [HttpPost("{id:long}")]
    public async Task<IActionResult> ImportOne([FromRoute] long id, CancellationToken ct = default)
    {
        // 1) IGDB’den detayını çek
        var detail = await _igdb.GetGameDetailAsync(id, ct);
        if (detail is null)
            return NotFound(new { message = "IGDB game not found", id });

        // 2) DB’ye upsert et
        await _svc.UpsertOneAsync(detail, ct);

        // 3) Özet dön
        return Ok(new { ok = true, importedId = detail.Id, name = detail.Name });
    }
}
