// Models/AppSettingsHistory.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class AppSettingsHistory
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public AppSettings Snapshot { get; set; } = default!;
}
