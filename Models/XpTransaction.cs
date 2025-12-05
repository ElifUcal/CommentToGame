using System;
using CommentToGame.enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models
{
    public class XpTransaction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        // User.Id ile birebir aynı tipte olsun (string + ObjectId)
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = default!;

        public XpSourceType SourceType { get; set; }

        // Eğer Game, Review vs Id'lerin de string/ObjectId ise bunu da string yap
        [BsonRepresentation(BsonType.ObjectId)]
        public string? SourceId { get; set; }

        /// <summary>
        /// Örn: "user:{UserId}:game:{GameId}:GameAddedToLibrary"
        /// Aynı event'ten 2 kere XP vermemek için.
        /// </summary>
        public string? UniqueKey { get; set; }

        public int Points { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
