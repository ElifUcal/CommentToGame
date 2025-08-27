using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/import/preview")]
[Produces("application/json")]
public class PreviewImportController : ControllerBase
{
    private readonly PreviewImportService _svc;
    private readonly ILogger<PreviewImportController> _logger;

    public PreviewImportController(PreviewImportService svc, ILogger<PreviewImportController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true });

    // Tek kayıt
    [HttpPost("one")]
    [Consumes("application/json")]
    public async Task<IActionResult> CommitOne(
        [FromBody] GameMerge.MergedGameDto dto,
        CancellationToken ct = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { ok = false, code = "bad_payload", message = "Geçersiz payload: name zorunlu." });

        try
        {
            var savedId = await _svc.UpsertOneAsync(dto, ct);
            return Ok(new { ok = true, id = savedId, name = dto.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CommitOne failed for {Name}", dto.Name);
            return MapMongoException(ex);
        }
    }

    // Toplu kayıt (kısmi başarı desteği ile)
    [HttpPost("commit")]
    [Consumes("application/json")]
    public async Task<IActionResult> CommitMany(
        [FromBody] List<GameMerge.MergedGameDto> payload,
        CancellationToken ct = default)
    {
        if (payload == null || payload.Count == 0)
            return BadRequest(new { ok = false, code = "bad_payload", message = "Boş payload." });

        try
        {
            // Servis zaten destekliyorsa bunu kullan
            var ids = await _svc.UpsertManyAsync(payload, ct);
            return Ok(new { ok = true, count = ids.Count, ids });
        }
        catch (Exception bulkEx)
        {
            _logger.LogWarning(bulkEx, "Bulk upsert failed; falling back to iterative upsert.");

            // Kademeli: tek tek dene, kısmi başarıyı raporla
            var successes = new List<string>();
            var failures = new List<object>();

            foreach (var dto in payload)
            {
                if (ct.IsCancellationRequested) break;

                if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                {
                    failures.Add(new { name = dto?.Name, code = "bad_item", message = "Geçersiz kayıt: name zorunlu." });
                    continue;
                }

                try
                {
                    var id = await _svc.UpsertOneAsync(dto, ct);
                    successes.Add(id);
                }
                catch (Exception exItem)
                {
                    _logger.LogError(exItem, "Item upsert failed for {Name}", dto.Name);
                    var mapped = BuildMongoErrorPayload(exItem);
                    failures.Add(new
                    {
                        name = dto.Name,
                        code = mapped.code,
                        message = mapped.message,
                        index = mapped.index,
                        field = mapped.field
                    });
                }
            }

            // 207 Multi-Status yerine 200 + detaylı rapor
            return Ok(new
            {
                ok = failures.Count == 0,
                succeeded = successes.Count,
                failed = failures.Count,
                ids = successes,
                errors = failures
            });
        }
    }

    // ----------------- Helpers -----------------

    private IActionResult MapMongoException(Exception ex)
    {
        var (code, message, index, field) = BuildMongoErrorPayload(ex);

        if (code == "duplicate_key")
        {
            return Conflict(new
            {
                ok = false,
                code,
                message,
                index,
                field,
                hint = "Bu değer daha önce eklenmiş olabilir (index collation: case-insensitive). Var olan kaydı kullanın ya da serviste collation ile upsert yapın."
            });
        }

        // Diğer Mongo veya genel hata → 500
        return StatusCode(500, new { ok = false, code, message });
    }

    /// <summary>
    /// Mongo hatasından anlamlı alanlar çıkar: duplicate, index adı, alan tahmini.
    /// </summary>
    private (string code, string message, string? index, string? field) BuildMongoErrorPayload(Exception ex)
    {
        // default
        string code = "server_error";
        string message = ex.Message;
        string? index = null;
        string? field = null;

        if (ex is MongoWriteException mwx)
        {
            if (mwx.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                code = "duplicate_key";
            }
            index = TryExtractIndexName(mwx.Message);
            field = TryGuessFieldFromIndex(index, mwx.Message);
            message = mwx.Message;
        }
        else if (ex is MongoCommandException mcx)
        {
            // E11000 duplicate key
            if (mcx.Code == 11000 || mcx.Message.Contains("E11000"))
                code = "duplicate_key";

            index = TryExtractIndexName(mcx.Message);
            field = TryGuessFieldFromIndex(index, mcx.Message);
            message = mcx.Message;
        }

        return (code, message, index, field);
    }

    private static string? TryExtractIndexName(string message)
    {
        // "... index: ux_genres_name ..." kısmını yakala
        var m = Regex.Match(message, @"index:\s*(\S+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? TryGuessFieldFromIndex(string? indexName, string message)
    {
        if (string.IsNullOrWhiteSpace(indexName)) return null;

        // Basit heuristik: ux_{collection}_{field} → field
        var idx = indexName!.ToLowerInvariant();
        if (idx.StartsWith("ux_"))
        {
            var parts = idx.Split('_');
            if (parts.Length >= 3)
                return parts.Last(); // örn. ux_genres_name → "name"
        }

        // "... dup key: { Name: ... }" deseninden alan yakala
        var m = Regex.Match(message, @"dup key:\s*\{\s*([A-Za-z0-9_]+)\s*:", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;

        return null;
    }
}
