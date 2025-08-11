using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Genre
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public string? Name { get; set; }

    // İstersen tutma, tek yön Game_Details -> GenreIds kalsın
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> GameIds { get; set; } = new();
}
