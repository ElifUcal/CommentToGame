using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;
public class Media
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public required string URL { get; set; }

    public required string Title { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public required string GalleryId { get; set; }

    [BsonIgnore]
    public Gallery? Gallery { get; set; }
}
