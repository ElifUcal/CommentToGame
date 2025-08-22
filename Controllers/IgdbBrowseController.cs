using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentToGame.Controllers
{
    [ApiController]
    [Route("api/igdb")]
    public sealed class IgdbBrowseController : ControllerBase
    {
        private readonly IIgdbClient _igdb;
        public IgdbBrowseController(IIgdbClient igdb) => _igdb = igdb;

        // GET /api/igdb/search?q=witcher&page=1&pageSize=20&dedupe=true&details=false
        [HttpGet("search")]
public async Task<IActionResult> Search(
    [FromQuery] string q,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] bool dedupe = true,
    [FromQuery] bool details = false,
    CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(q)) return BadRequest("q gerekli");
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 50);

    var data = await _igdb.SearchGamesAsync(q, page, pageSize, ct);
    var items = data.Results.AsEnumerable();

    if (dedupe)
    {
        items = items
            .GroupBy(x => (x.Name ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(i => i.Id).First());
    }

    if (details)
    {
        // Tümüyle kart nesnesini döndür
        return Ok(new { items, next = data.Next });
    }

    // Sadece id+name döndür
    var shaped = items.Select(x => new { x.Id, x.Name });
    return Ok(new { items = shaped, next = data.Next });
}


        // GET /api/igdb/detail/12345 (DBye yazmaz)
        [HttpGet("detail/{id:long}")]
        public async Task<IActionResult> Detail([FromRoute] long id, CancellationToken ct = default)
        {
            var d = await _igdb.GetGameDetailAsync(id, ct);
            return d is null ? NotFound() : Ok(d);
        }
    }
}
