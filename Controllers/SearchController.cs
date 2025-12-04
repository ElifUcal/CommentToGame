using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

  [HttpGet]
public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
{
    

    try
    {
        var result = await _searchService.GlobalSearchAsync(q, ct);
        
        return Ok(result);
    }
    catch (Exception ex)
    {
        
        return StatusCode(500, new { error = ex.Message });
    }
}


}
