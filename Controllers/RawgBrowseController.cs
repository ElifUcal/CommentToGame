using System;
using System.Linq;
using System.Threading.Tasks;
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
            var items = data.Results.Select(x => new { id = x.Id, name = x.Name });

            // RAWG genelde "next" için URL döner yoksa null olur.
            var next = string.IsNullOrEmpty(data.Next) ? null : data.Next;
            return Ok(new { items, next });
        }

        // (opsiyonel) tek oyun detayı – DBye yazmaz
        // GET /api/rawg/detail/3498
        [HttpGet("detail/{id:int}")]
        public async Task<IActionResult> Detail([FromRoute] int id)
        {
            var d = await _rawg.GetGameDetailAsync(id);
            return d is null ? NotFound() : Ok(d);
        }
    }
}
