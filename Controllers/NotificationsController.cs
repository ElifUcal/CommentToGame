using CommentToGame.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace YourProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Kullanıcının bildirim listesi
        // GET /api/notifications?onlyUnread=true&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetForCurrentUser(
            [FromQuery] bool onlyUnread = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var (items, totalCount) = await _notificationService
                .GetForUserAsync(userId, onlyUnread, page, pageSize, ct);

            return Ok(new
            {
                items,
                totalCount,
                page,
                pageSize
            });
        }

        // Unread count
        // GET /api/notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(userId, ct);
            return Ok(new { count });
        }

        // Tek bildirimi okundu işaretle
        // POST /api/notifications/{id}/read
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(string id, CancellationToken ct = default)
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _notificationService.MarkAsReadAsync(userId, id, ct);
            return NoContent();
        }

        // Hepsini okundu işaretle
        // POST /api/notifications/mark-all-read
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(userId, ct);
            return NoContent();
        }
    }
}
