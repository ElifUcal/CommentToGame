using System.Net;
using CommentToGame.Services;
using CommentToGame.Models;

namespace CommentToGame.Middleware;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    public ExceptionLoggingMiddleware(RequestDelegate next) { _next = next; }

    public async Task Invoke(HttpContext ctx, ISystemLogger slog)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            var user = ctx.User?.Identity?.Name ?? "anonymous";
            await slog.ErrorAsync(SystemLogCategory.System, $"Unhandled exception: {ex.Message}", user, new Dictionary<string,string>{
                ["path"] = ctx.Request.Path,
                ["method"] = ctx.Request.Method
            });

            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "Unexpected server error." });
        }
    }
}
