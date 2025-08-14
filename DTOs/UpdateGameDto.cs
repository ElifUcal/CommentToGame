namespace CommentToGame.DTOs;

public sealed class UpdateGameDto
{
    // --- Game (üst) ---
    public string? Name { get; set; }                 // optional rename
    public DateTime? ReleaseDate { get; set; }
    public int? Metacritic { get; set; }
    public int? GgDbRating { get; set; }              // /100 beklenir
    public string? MainImage { get; set; }
    public int? Popularity { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public string? Studio { get; set; }               // NEW
    public string? About { get; set; }

    // İsim listeleri (null => dokunma, [] => temizle)
    public List<string>? AgeRatings { get; set; }
    public List<string>? Dlcs { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Genres { get; set; }         // isim olarak
    public List<string>? Platforms { get; set; }      // isim olarak

    // Sistem gereksinimi (metin)
    public string? MinRequirement { get; set; }
    public string? RecRequirement { get; set; }

    // --- NEW: Diller & içerik uyarıları ---
    public List<string>? AudioLanguages { get; set; }
    public List<string>? Subtitles { get; set; }
    public List<string>? InterfaceLanguages { get; set; }
    public List<string>? ContentWarnings { get; set; }

    // --- NEW: Mağaza linkleri ---
    public List<StoreLinkDto>? StoreLinks { get; set; }

    // --- NEW: Time To Beat (saat cinsinden) ---
    public int? TimeToBeatHastily { get; set; }
    public int? TimeToBeatNormally { get; set; }
    public int? TimeToBeatCompletely { get; set; }

    // --- NEW: Engine(ler) & Ödüller ---
    public List<string>? Engines { get; set; }
    public List<string>? Awards { get; set; }
}

public sealed class StoreLinkDto
{
    public int? StoreId { get; set; }
    public string? Store { get; set; }
    public string? Slug { get; set; }
    public string? Domain { get; set; }
    public string? Url { get; set; }
    public string? ExternalId { get; set; }
}
