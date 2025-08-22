using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin")] // sadece Admin
    public IActionResult Dashboard()
    {
        return Ok("Admin panel verisi 🔐");
    }
}
