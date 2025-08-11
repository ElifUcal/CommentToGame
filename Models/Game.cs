using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Game
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public required string Game_Name { get; set; }

    public DateTime? Release_Date { get; set; }
    public string? Studio { get; set; }

    public List<string> Cast { get; set; } = new();
    public string? Soundtrack { get; set; }
    public List<string> Crew { get; set; } = new();

    // Companies ayrı koleksiyon ise Id listesi
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string>? CompanyIds { get; set; }

    public int? GgDb_Rating { get; set; }
    public int? Metacritic_Rating { get; set; }
    public int? Popularity { get; set; }

    public string? Main_image_URL { get; set; }
    public string? Main_video_URL { get; set; }

    // Time To Beat ayrı koleksiyon ise
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Time_To_BeatId { get; set; }

    [BsonIgnore]
    public Time_To_Beat? Time_To_Beat { get; set; }
}
