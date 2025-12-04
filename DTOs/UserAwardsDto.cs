namespace CommentToGame.DTOs;
public class UserAwardsDto
{
    public int GamesPlayedCount { get; set; }          // progress >= 30
    public int CompletedGamesCount { get; set; }       // progress >= 80
    public double CompletionRatePercent { get; set; }  // completed / gamesPlayed * 100

    public int ReviewedGamesCount { get; set; }        // oynadığı oyunlardan kaçına review
    public double ReviewApprovalRatePercent { get; set; } // reviewed / gamesPlayed * 100
}
