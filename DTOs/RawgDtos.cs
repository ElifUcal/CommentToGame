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

    // requirements için wrapperı genişletiyoruz
    public List<RawgPlatformWrapper> Platforms { get; set; } = new();

    public int? Metacritic { get; set; }
    public double? Rating { get; set; }

    [JsonPropertyName("background_image")]
    public string? BackgroundImage { get; set; }

    [JsonPropertyName("ratings_count")]
    public int? RatingsCount { get; set; }

    [JsonPropertyName("added")]
    public int? Added { get; set; }   // "popülerlik" için bir proxy

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

    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = new();

    [JsonPropertyName("languages_audio")]
    public List<string> LanguagesAudio { get; set; } = new();

    [JsonPropertyName("languages_subtitles")]
    public List<string> LanguagesSubtitles { get; set; } = new();

    [JsonPropertyName("content_warnings")]
    public List<string> ContentWarnings { get; set; } = new();

    [JsonPropertyName("game_engines")]
    public List<RawgNameWrapper> GameEngines { get; set; } = new();


    [JsonPropertyName("engines")]
    public List<RawgNameWrapper> EnginesAlt { get; set; } = new();



    [JsonPropertyName("awards")]
    public List<RawgAward> Awards { get; set; } = new();

    [JsonPropertyName("stores")]
    public List<RawgGameStoreItem> Stores { get; set; } = new();


}

public class RawgGameEngine
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class RawgAward
{
    public string? Name { get; set; }
    public int? Year { get; set; }
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

    // Sadece PC için döner (minimum/recommended) konsollarda genelde null
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

public class RawgGameStoreItem
{
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    // Bazı yanıtlarda sadece bu geliyor
    [JsonPropertyName("store_id")]
    public int? StoreId { get; set; }

    // Bazı yanıtlarda komple store objesi var
    [JsonPropertyName("store")]
    public RawgStoreInfo? Store { get; set; }
}

public class RawgStoreInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public string? Domain { get; set; }   // Bu alan her zaman dolmayabilir
}

public class RawgPagedCreators
{
    public int Count { get; set; }
    public string? Next { get; set; }
    public List<RawgCreator> Results { get; set; } = new();
}

public class RawgCreator
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public List<RawgCreatorPosition> Positions { get; set; } = new();
}

public class RawgCreatorPosition
{
    public string Name { get; set; } = default!;
}

