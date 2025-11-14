using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }   // Bildirimin sahibi (user veya admin)

        public string Type { get; set; }     // Örn: NEW_COMMENT, FRIEND_REQUEST, ADMIN_ALERT

        public string Title { get; set; }
        public string Message { get; set; }

        // Esnek ekstra bilgiler için (gameId, commentId vs.)
        public BsonDocument Data { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }
}
