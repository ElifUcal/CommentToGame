using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Gallery
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    // Bir oyuna ait birden fazla görsel
    public List<string> Urls { get; set; } = new();

    [BsonRepresentation(BsonType.ObjectId)]
    public required string GameId { get; set; }

    [BsonIgnore]
    public Game? Game { get; set; }
}
