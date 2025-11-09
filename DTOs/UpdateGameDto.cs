using CommentToGame.Models;

namespace CommentToGame.DTOs
{
    public sealed class UpdateGameDto
    {
        // --- Game (üst) ---
        public string? Name { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public int? Metacritic { get; set; }
        public int? GgDbRating { get; set; }
        public string? MainImage { get; set; }
        public int? Popularity { get; set; }
        public string? Developer { get; set; }
        public string? Publisher { get; set; }
        public string? Studio { get; set; }
        public string? About { get; set; }

        // İsim listeleri
        public List<string>? AgeRatings { get; set; }
        public List<DLCitem>? Dlcs { get; set; }

        public List<string>? Tags { get; set; }
        public List<string>? Genres { get; set; }
        public List<string>? Platforms { get; set; }

        // Sistem gereksinimi (metin)
        public string? MinRequirement { get; set; }
        public string? RecRequirement { get; set; }

        // Diller & içerik uyarıları
        public List<string>? AudioLanguages { get; set; }
        public List<string>? Subtitles { get; set; }
        public List<string>? InterfaceLanguages { get; set; }
        public List<string>? ContentWarnings { get; set; }

        // 🔴 TEK tanım — Store Links
        public List<StoreLinkDto>? StoreLinks { get; set; }

        // Time To Beat
        public int? TimeToBeatHastily { get; set; }
        public int? TimeToBeatNormally { get; set; }
        public int? TimeToBeatCompletely { get; set; }

        // Engine(ler) & Ödüller
        public List<string>? Engines { get; set; }
        public List<AwardInfo>? Awards { get; set; }

        public List<string>? Cast { get; set; }
        public List<string>? Crew { get; set; }
    }
}
