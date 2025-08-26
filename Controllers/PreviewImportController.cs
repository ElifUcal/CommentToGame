using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/import/preview")]
public class PreviewImportController : ControllerBase
{
    private readonly PreviewImportService _svc;

    public PreviewImportController(PreviewImportService svc)
    {
        _svc = svc;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true });

    // Tek kayıt
    [HttpPost("one")]
    public async Task<IActionResult> CommitOne(
        [FromBody] GameMerge.MergedGameDto dto,
        CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Geçersiz payload: name zorunlu." });

        var savedId = await _svc.UpsertOneAsync(dto, ct);
        return Ok(new { ok = true, id = savedId, name = dto.Name });
    }

    // Toplu kayıt
    [HttpPost("commit")]
    public async Task<IActionResult> CommitMany(
        [FromBody] List<GameMerge.MergedGameDto> payload,
        CancellationToken ct = default)
    {
        if (payload == null || payload.Count == 0)
            return BadRequest(new { message = "Boş payload." });

        var ids = await _svc.UpsertManyAsync(payload, ct);
        return Ok(new { ok = true, count = ids.Count, ids });
    }
}
