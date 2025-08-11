using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Time_To_Beat
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public string? Type { get; set; }
    public int? Main_Story { get; set; }
    public int? Main_Extras { get; set; }
    public int? Completionist { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public required string GameId { get; set; }

    [BsonIgnore]
    public Game? Game { get; set; }
}
