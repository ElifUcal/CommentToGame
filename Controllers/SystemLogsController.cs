using System.Globalization;
using System.Text;
using CommentToGame.DTOs;
using CommentToGame.Models;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentToGame.Controllers;

[ApiController]
[Route("api/logs")]
public class SystemLogsController : ControllerBase
{
    private readonly SystemLogService _svc;
    public SystemLogsController(SystemLogService svc) { _svc = svc; }

    [HttpGet]
    public async Task<Paged<SystemLog>> Get([FromQuery] LogQuery q, CancellationToken ct)
        => await _svc.QueryAsync(q, ct);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] LogQuery q, CancellationToken ct)
    {
        q.Page = 1; q.PageSize = 10_000;
        var res = await _svc.QueryAsync(q, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Time,Level,Category,Message,User");
        foreach (var x in res.Items)
        {
            var t = x.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var msg = x.Message.Replace("\"","\"\"");
            sb.AppendLine($"{t},{x.Level},{x.Category},\"{msg}\",{x.User}");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"system-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
