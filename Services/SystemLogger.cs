using CommentToGame.Models;

namespace CommentToGame.Services;

public class SystemLogger : ISystemLogger
{
    private readonly SystemLogService _logs;
    public SystemLogger(SystemLogService logs) { _logs = logs; }

    public Task LogAsync(SystemLogLevel level, SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default)
        => _logs.InsertAsync(new SystemLog { Time = DateTime.UtcNow, Level = level, Category = category, Message = message, User = user, Meta = meta }, ct);

    public Task InfoAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default)
        => LogAsync(SystemLogLevel.Info, category, message, user, meta, ct);

    public Task WarningAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default)
        => LogAsync(SystemLogLevel.Warning, category, message, user, meta, ct);

    public Task ErrorAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default)
        => LogAsync(SystemLogLevel.Error, category, message, user, meta, ct);

    public Task SuccessAsync(SystemLogCategory category, string message, string user = "system", Dictionary<string,string>? meta = null, CancellationToken ct = default)
        => LogAsync(SystemLogLevel.Success, category, message, user, meta, ct);
}
