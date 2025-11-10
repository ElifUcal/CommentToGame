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
    Console.WriteLine($"🎯 Search endpoint triggered with query: {q}");

    try
    {
        var result = await _searchService.GlobalSearchAsync(q, ct);
        Console.WriteLine("✅ Search completed successfully");
        return Ok(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ SearchController CATCH: " + ex);
        return StatusCode(500, new { error = ex.Message });
    }
}


}
