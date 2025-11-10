using System.Net;
using CommentToGame.Services;
using CommentToGame.Models;

namespace CommentToGame.Middleware;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    public ExceptionLoggingMiddleware(RequestDelegate next) { _next = next; }

   public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔥 Middleware yakaladı: {ex.Message}");
                Console.WriteLine(ex.StackTrace);

                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

}
