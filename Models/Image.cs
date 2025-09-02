using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Image
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    public required string URL { get; set; }

    public required string Title { get; set; }

    public List<MetaData>? MetaDatas { get; set; }



}
