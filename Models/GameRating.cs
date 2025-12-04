using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace CommentToGame.Models;
public class GameRating
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string UserId { get; set; } = default!;
    public string GameId { get; set; } = default!;

    // 0–10 arası puanlar
    public double MusicAndSound { get; set; }
    public double StoryAndWriting { get; set; }
    public double Gameplay { get; set; }
    public double Visuals { get; set; }
    public double BugsAndStability { get; set; }
    public double Replayability { get; set; }

    public double OverallScore { get; set; }   // (istenirse)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
