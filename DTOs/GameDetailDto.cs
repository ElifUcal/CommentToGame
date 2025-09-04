namespace CommentToGame.DTOs;

public class GameDetailDto
{
    public string Id { get; set; } = default!;

    // Game (temel)
    public string Title { get; set; } = default!;
    public DateTime? ReleaseDate { get; set; }
    public string? Studio { get; set; }
    public int? GgdbRating { get; set; }
    public int? MetacriticRating { get; set; }
    public string? Cover { get; set; }
    public string? Video { get; set; }

    // Game_Details
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Platforms { get; set; } = new();
    public string? Story { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Dlcs { get; set; } = new();
    public List<string> Crew { get; set; } = new();
    public List<string>? Awards { get; set; }
    public List<string> GameEngine { get; set; } = new();

    public string? MinRequirements { get; set; }
    public string? RecRequirements { get; set; }
    public List<string> ContentWarnings { get; set; } = new();
    public List<string> AgeRatings { get; set; } = new();

    public List<string> AudioLanguages { get; set; } = new();
    public List<string> SubtitleLanguages { get; set; } = new();
    public List<string> InterfaceLanguages { get; set; } = new();

    // Store links
    public List<StoreLinkDto> StoreLinks { get; set; } = new();

    public List<string> Soundtrack { get; set; } = new();

    public List<string> Screenshots { get; set; } = new();
    public List<TrailerDto> Trailers { get; set; } = new();

    public List<ImageDto>? Images { get; set; }     // root’ta düz liste
    public List<VideoDto>? Videos { get; set; }
    public GalleryDto? Gallery { get; set; }        // opsiyonel, geriye uyumluluk için

    public int? TimeToBeat_Hastily { get; set; }
    public int? TimeToBeat_Normally { get; set; }
    public int? TimeToBeat_Completely { get; set; }

}


public sealed class MetaDataDto
{
    public string? Label { get; set; }
    public string? Value { get; set; }
}

public sealed class ImageDto
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public List<MetaDataDto>? MetaDatas { get; set; }
}

public sealed class VideoDto
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string? YouTubeId { get; set; } // DB’de yoksa null kalır
    public List<MetaDataDto>? MetaDatas { get; set; }
}

public sealed class GalleryDto
{
    public List<ImageDto> Images { get; set; } = new();
    public List<VideoDto> Videos { get; set; } = new();
}
