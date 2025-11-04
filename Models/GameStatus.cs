using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models
{
    public class GameStatus { 
        
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        public string GameId { get; set; } 
        public string UserId { get; set; } 
        public string Status { get; set; } }

}