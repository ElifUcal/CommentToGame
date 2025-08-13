namespace CommentToGame.DTOs
{
    /// <summary>
    /// Edit formu için giriş/çıkış DTO'su.
    /// Sadece gönderilen (null olmayan) alanlar güncellenir.
    /// Listeler gönderilirse komple REPLACE yapılır.
    /// Min/Rec text boş string gönderilirse ilişki sıfırlanır (null).
    /// </summary>
    public sealed class UpdateGameDto
    {
        // Game
        public string? Name { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public int? Metacritic { get; set; }
        public int? GgDbRating { get; set; }
        public string? MainImage { get; set; }
        public int? Popularity { get; set; }

        // Details
        public string? Developer { get; set; }
        public string? Publisher { get; set; }
        public string? About { get; set; }

        public List<string>? AgeRatings { get; set; }
        public List<string>? Dlcs { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Genres { get; set; }     // names
        public List<string>? Platforms { get; set; }  // names

        public string? MinRequirement { get; set; }
        public string? RecRequirement { get; set; }
    }
}