namespace CommentToGame.DTOs;
public class UpsertGameRatingRequest
{
    public double MusicAndSound { get; set; }
    public double StoryAndWriting { get; set; }
    public double Gameplay { get; set; }
    public double Visuals { get; set; }
    public double BugsAndStability { get; set; }
    public double Replayability { get; set; }
}

public class GameRatingDto
{
    public string GameId { get; set; } = default!;
    public string UserId { get; set; } = default!;

    public double MusicAndSound { get; set; }
    public double StoryAndWriting { get; set; }
    public double Gameplay { get; set; }
    public double Visuals { get; set; }
    public double BugsAndStability { get; set; }
    public double Replayability { get; set; }

    public double OverallScore { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GameRatingSummaryDto
{
    public string GameId { get; set; } = default!;

    public double AvgMusicAndSound { get; set; }
    public double AvgStoryAndWriting { get; set; }
    public double AvgGameplay { get; set; }
    public double AvgVisuals { get; set; }
    public double AvgBugsAndStability { get; set; }
    public double AvgReplayability { get; set; }

    public double AvgOverallScore { get; set; }
    public int TotalRaters { get; set; }
}
