using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Gallery
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    [BsonRepresentation(BsonType.ObjectId)]
    public required string GameId { get; set; }

    [BsonIgnore]
    public Game? Game { get; set; }

    public List<Image>? Images { get; set; }

    public List<Video>? Videos { get; set; }



    
}
