using System.Text.Json.Serialization;

namespace CommentToGame.DTOs;

public class RawgPaged<T>
{
    public int Count { get; set; }
    public string? Next { get; set; }
    public List<T> Results { get; set; } = new();
}

public class RawgTag
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Slug { get; set; }
}

public class RawgGameSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Released { get; set; }

    public List<RawgNameWrapper> Genres { get; set; } = new();

    // requirements için wrapper'ı genişletiyoruz
    public List<RawgPlatformWrapper> Platforms { get; set; } = new();

    public int? Metacritic { get; set; }
    public double? Rating { get; set; }

    [JsonPropertyName("background_image")]
    public string? BackgroundImage { get; set; }

    [JsonPropertyName("ratings_count")]
    public int? RatingsCount { get; set; }

    [JsonPropertyName("added")]
    public int? Added { get; set; }   // "popülerlik" için güzel bir proxy

    public string? Slug { get; set; }
}

public class RawgGameDetail : RawgGameSummary
{
    public List<RawgNameWrapper> Developers { get; set; } = new();
    public List<RawgNameWrapper> Publishers { get; set; } = new();

    [JsonPropertyName("description_raw")]
    public string? DescriptionRaw { get; set; }  // “About”

    // Yaş derecelendirmeleri
    [JsonPropertyName("esrb_rating")]
    public RawgEsrbRating? EsrbRating { get; set; }

    [JsonPropertyName("age_ratings")]
    public List<RawgAgeRating> AgeRatings { get; set; } = new();

    public int? Playtime { get; set; }   // saat
    public string? Website { get; set; }

     [JsonPropertyName("tags")]
    public List<RawgTag> Tags { get; set; } = new();
}

public class RawgNameWrapper
{
    public string Name { get; set; } = default!;
    public string? Slug { get; set; }
    public int? Id { get; set; }
}

// Platform + (opsiyonel) gereksinimler
public class RawgPlatformWrapper
{
    public RawgNameWrapper Platform { get; set; } = new();

    // Sadece PC için döner (minimum/recommended), konsollarda genelde null
    [JsonPropertyName("requirements")]
    public RawgRequirements? Requirements { get; set; }
}

public class RawgRequirements
{
    public string? Minimum { get; set; }
    public string? Recommended { get; set; }
}

// ESRB ve yaş derecelendirme
public class RawgEsrbRating
{
    public int? Id { get; set; }
    public string? Name { get; set; }   // e.g., "Mature"
    public string? Slug { get; set; }   // e.g., "mature"
}

public class RawgAgeRating
{
    public int Id { get; set; }         // RAWG age rating id
    public string? Name { get; set; }   // e.g., "Mature"
    public string? Slug { get; set; }   // e.g., "mature"
}
