namespace CommentToGame.Controllers
{
    using CommentToGame.DTOs;
    using CommentToGame.Services;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/games")] // GET/PUT by-name
    public sealed class GameEditController : ControllerBase
    {
        private readonly GameEditService _edit;
        public GameEditController(GameEditService edit) { _edit = edit; }

        // Formu doldurmalık (tam isim, case-insensitive)
        [HttpGet("by-name/{name}")]
        public async Task<IActionResult> GetByName([FromRoute] string name, CancellationToken ct)
        {
            var dto = await _edit.GetEditableByNameAsync(name, ct);
            if (dto is null) return NotFound(new { message = "Game not found" });
            return Ok(dto);
        }

        // Parçalı arama (contains) → en iyi tek eşleşme
        [HttpGet("by-name-like/{name}")]
        public async Task<IActionResult> GetByNameLike([FromRoute] string name, CancellationToken ct)
        {
            var dto = await _edit.GetEditableByNameLikeAsync(name, ct: ct);
            if (dto is null) return NotFound(new { message = "No match" });
            return Ok(dto);
        }

        // PUT isme göre merge update (göndermediklerine dokunmadan)
        [HttpPut("by-name/{name}")]
        public async Task<IActionResult> PutByName([FromRoute] string name, [FromBody] UpdateGameDto body, CancellationToken ct)
        {
            if (body is null) return BadRequest(new { message = "Body gerekli" });
            var ok = await _edit.UpdateByNameAsync(name, body, ct);
            if (!ok) return NotFound(new { message = "Game not found" });
            return Ok(new { ok = true, name });
        }
    }
}