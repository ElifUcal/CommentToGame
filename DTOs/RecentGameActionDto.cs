public class RecentGameActionDto
{
    public string GameId { get; set; }
    public string ActionType { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? Title { get; set; }
    public string? Developer { get; set; }
    public int? ReleaseYear { get; set; }
    public int MatchScore { get; set; }
    public double StarRating { get; set; }
    public string? ImageUrl { get; set; }

    // 🔥 yeni
    public int? ProgressPercent { get; set; }
}
