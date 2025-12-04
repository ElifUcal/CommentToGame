namespace CommentToGame.DTOs;
public class UserActivitySubRatingDto
{
    public string Label { get; set; } = default!;
    public double Value { get; set; }
    public double Max { get; set; }
}

public class UserActivityItemDto
{
    public string Id { get; set; } = default!;          // activity id
    public string Type { get; set; } = default!;        // "progress" | "liked" | "review" | "loved" | "disliked" | "rating" | "plan-to-play"

    public string GameId { get; set; } = default!;
    public string GameName { get; set; } = default!;
    public string? GameCover { get; set; }

    public DateTime Timestamp { get; set; }

    public int? ProgressPercent { get; set; }           // progress
    public string? ReviewText { get; set; }             // review
    public double? ReviewRating { get; set; }           // review (0–10)
    public double? OverallRating { get; set; }          // GameRating.OverallScore

    public List<UserActivitySubRatingDto>? SubRatings { get; set; } // rating alt başlıklar
}

public class UserActivityResponseDto
{
    public List<UserActivityItemDto> Items { get; set; } = new();
    public int Total { get; set; }
}
