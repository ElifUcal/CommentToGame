using CommentToGame.DTOs;
using CommentToGame.enums;

public interface IGamificationService
{
    Task AddXpAsync(string userId, XpSourceType sourceType, string? sourceId = null, string? uniqueKey = null);

    Task<UserGamificationSummaryDto> GetUserSummaryAsync(string userId);
}
