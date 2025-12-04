using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    [BsonElement("userName")]
    public required string UserName { get; set; }

    [BsonElement("email")]
    public required string Email { get; set; }

    [BsonElement("passwordHash")]
    public required string PasswordHash { get; set; }

    [BsonElement("refreshToken")]
    public string? RefreshToken { get; set; }

    [BsonElement("refreshTokenExpiryTime")]
    public DateTime? RefreshTokenExpiryTime { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("userType")]
    public UserType UserType { get; set; } = UserType.User; // varsayılan: User

    [BsonElement("createdat")]
    public required DateTime Createdat { get; set; } = DateTime.Now;

    [BsonElement("Birthdate")]
    public required DateTime Birthdate { get; set; }

    [BsonElement("country")]
    public required string Country { get; set; }

    [BsonElement("ProfileImageUrl")]
    public string? ProfileImageUrl { get; set; }

    public bool isBanned { get; set; }


    public string? BannerUrl { get; set; }

    public string[]? Platforms { get; set; }

    public string? Badge { get; set; }

    public string[]? Title { get; set; }

    public string? City { get; set; }

    public string[]? ContactUrl { get; set; }

    public string? About { get; set; }

    public string[]? Skills { get; set; }

    public WorkExperience[]? Experiences { get; set; }

    public ProjectItems[]? Projects { get; set; }


    public Educations[]? Educations { get; set; }

    public Awards[]? Awards { get; set; }

    public List<string>? FavoriteGenres { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public List<string>? FavConsoles { get; set; }

    public Equipment? Equipment { get; set; }

    public string? CareerGoal { get; set; }
}


public class WorkExperience
{
    public string? Title { get; set; }
    public string? Company { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? FinishDate { get; set; }

    public bool? isCurrent { get; set; }
}


public class ProjectItems
{
    public string? Title { get; set; }
    public string? Game { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? FinishDate { get; set; }

    public bool? isCurrent { get; set; }
}

public class Educations
{
    public string? Department { get; set; }
    public string? University { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? FinishDate { get; set; }
    public bool? isCurrent { get; set; }
}

public class Awards
{
    public string? Name { get; set; }
    public string? Giving_Organization { get; set; }

    public DateTime? ReceivedDate { get; set; }
    public bool? isCurrent { get; set; }
}

public class Equipment
{
    public string? OS { get; set; }
    public string? CPU { get; set; }

    public string? GPU { get; set; }

    public string? RAM { get; set; }

    public string? Motherboard { get; set; }
    public string? Case { get; set; }

    public string? Storage { get; set; }

    public string? Keyboard { get; set; }

    public string? Mouse { get; set; }

    public string? Monitor { get; set; }
}