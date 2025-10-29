using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models
{
    public class Reviews
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        [BsonRepresentation(BsonType.ObjectId)]
        public required string GameId { get; set; }

        [BsonIgnore]
        public Game? Game { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public required string UserId { get; set; }

        [BsonIgnore]
        public User? User { get; set; }

        public int StarCount { get; set; }
        
        public string? Comment { get; set; }
    
        public bool isSpoiler { get; set; }

        public DateTime TodayDate { get; set; } = DateTime.Now;
    }
}