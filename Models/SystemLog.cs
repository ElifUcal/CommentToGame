using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

// Microsoft.Extensions.Logging.LogLevel ile karışmasın
public enum SystemLogLevel { Error=1, Warning=2, Info=3, Success=4 }

// Frontend dropdown’larıyla birebir aynı:
public enum SystemLogCategory { Authentication=1, UserActions=2, GameManagement=3, System=4, Security=5 }

public class SystemLog
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public DateTime Time { get; set; }            // UTC sakla
    public SystemLogLevel Level { get; set; }
    public SystemLogCategory Category { get; set; }

    public string Message { get; set; } = "";
    public string User { get; set; } = "system";
    public Dictionary<string, string>? Meta { get; set; }
}
