using CommentToGame.DTOs;
using CommentToGame.Models;
using MongoDB.Driver;
using CommentToGame.Data; // MongoDbService

namespace CommentToGame.Services;

public class SystemLogService
{
    private readonly IMongoCollection<SystemLog> _col;

    public SystemLogService(MongoDbService mongo)
    {
        var db = mongo.Database; // kendi MongoDbService’inde Database özelliği olmalı
        _col = db.GetCollection<SystemLog>("SystemLogs");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var keys = Builders<SystemLog>.IndexKeys
            .Descending(x => x.Time)
            .Ascending(nameof(SystemLog.Level))
            .Ascending(nameof(SystemLog.Category));

        _col.Indexes.CreateOne(new CreateIndexModel<SystemLog>(keys));
    }

    public async Task<Paged<SystemLog>> QueryAsync(LogQuery q, CancellationToken ct = default)
    {
        var fb = Builders<SystemLog>.Filter;
        var filters = new List<FilterDefinition<SystemLog>>();

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var rx = new MongoDB.Bson.BsonRegularExpression(q.Q, "i");
            filters.Add(fb.Regex(x => x.Message, rx));
        }

        if (q.Levels?.Length > 0)
        {
            var wanted = q.Levels.Select(s => Enum.Parse<SystemLogLevel>(s, true)).ToArray();
            filters.Add(fb.In(x => x.Level, wanted));
        }

        if (q.Categories?.Length > 0)
        {
            var wanted = q.Categories.Select(s => Enum.Parse<SystemLogCategory>(s, true)).ToArray();
            filters.Add(fb.In(x => x.Category, wanted));
        }

        if (q.From.HasValue) filters.Add(fb.Gte(x => x.Time, q.From.Value));
        if (q.To.HasValue)   filters.Add(fb.Lte(x => x.Time, q.To.Value));

        var filter = filters.Count > 0 ? fb.And(filters) : FilterDefinition<SystemLog>.Empty;

        var find = _col.Find(filter).SortByDescending(x => x.Time);

        var total = await find.CountDocumentsAsync(ct);
        var items = await find.Skip((q.Page - 1) * q.PageSize).Limit(q.PageSize).ToListAsync(ct);

        return new Paged<SystemLog> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
    }

    public Task InsertAsync(SystemLog log, CancellationToken ct = default)
        => _col.InsertOneAsync(log, cancellationToken: ct);

    // İSTEMİYORSAN ÇAĞIRMA: Seed sadece demo içindi
    public async Task SeedDemoAsync()
    {
        if (await _col.EstimatedDocumentCountAsync() > 0) return;

        var now = DateTime.UtcNow;
        var demo = new[]
        {
            new SystemLog { Time = now.AddMinutes(-1),  Level = SystemLogLevel.Error,   Category = SystemLogCategory.Authentication, Message = "Failed login attempt from IP 192.168.1.100",      User = "system" },
            new SystemLog { Time = now.AddMinutes(-6),  Level = SystemLogLevel.Info,    Category = SystemLogCategory.UserActions,    Message = "User @gamer123 updated their profile",           User = "gamer123" },
            new SystemLog { Time = now.AddMinutes(-11), Level = SystemLogLevel.Warning, Category = SystemLogCategory.System,         Message = "Database connection pool approaching limit",     User = "system" },
            new SystemLog { Time = now.AddMinutes(-16), Level = SystemLogLevel.Success, Category = SystemLogCategory.GameManagement, Message = "Game \"Cyberpunk 2077\" successfully approved",  User = "admin" },
            new SystemLog { Time = now.AddMinutes(-21), Level = SystemLogLevel.Error,   Category = SystemLogCategory.Security,       Message = "Potential SQL injection attempt detected",       User = "system" },
        };

        await _col.InsertManyAsync(demo);
    }
}
