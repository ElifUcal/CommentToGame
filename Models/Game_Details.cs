using CommentToGame.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class Game_Details
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    [BsonRepresentation(BsonType.ObjectId)]
    public required string GameId { get; set; }

    [BsonIgnore]
    public Game? Game { get; set; }

    public string? Developer { get; set; }

    // Master koleksiyonlardan Id listesi
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string>? GenreIds { get; set; }

    public string? Publisher { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string>? PlatformIds { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? Price { get; set; }

    public string? Story { get; set; }
    public string? Franchise { get; set; }
    public string? Game_engine { get; set; }
    public string? Director { get; set; }
    public string? Composer { get; set; }
    public string? Estimated_Playtime { get; set; }

    public List<string> Tags { get; set; } = new();
    public List<string>? Awards { get; set; }
    public List<string> DLCs { get; set; } = new();
    public string? InspiredBy { get; set; }

    public List<string> Audio_Language { get; set; } = new();
    public List<string> Subtitles { get; set; } = new();
    public List<string> Interface_Language { get; set; } = new();
    public List<string> Content_Warnings { get; set; } = new();
    public List<string>? Age_Ratings { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? MinRequirementId { get; set; }

    [BsonIgnore] public MinRequirement? MinRequirement { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? RecRequirementId { get; set; }


    [BsonIgnore] public RecRequirement? RecRequirement { get; set; }

    public int? TimeToBeat_Hastily { get; set; }
    public int? TimeToBeat_Normally { get; set; }
    public int? TimeToBeat_Completely { get; set; }

    public List<StoreLink> Store_Links { get; set; } = new();

    public List<string> Engines { get; set; } = new();

    public List<string> Screenshots { get; set; } = new();
    public List<TrailerDto> Trailers { get; set; } = new();

    public string? GameDirector { get; set; }

    public List<string>? ScenarioWriters { get; set; }

    public string? ArtDirector { get; set; }

    public List<string>? LeadActors { get; set; }

    public List<string>? VoiceActors { get; set; }
    
    public string? MusicComposer { get; set; }

    public List<string>? Cinematics_VfxTeam { get; set; }


}
