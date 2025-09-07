using CommentToGame.Models;

namespace CommentToGame.Services;

public interface ISystemLogger
{
    Task LogAsync(SystemLogLevel level, SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default);
    Task InfoAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default);
    Task WarningAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default);
    Task ErrorAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default);
    Task SuccessAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default);
}
