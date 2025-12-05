namespace CommentToGame.DTOs;

public class UserGamificationSummaryDto
{
    public int Level { get; set; }
    public int TotalXp { get; set; }

    public int CurrentLevelXp { get; set; }   // 0-99 arası
    public int XpToNextLevel { get; set; }    // 100 - CurrentLevelXp

    // İstersen su anda doldurmayacağız, ama ileride:
    public List<NextActionSuggestionDto> Suggestions { get; set; } = new();
}

public class NextActionSuggestionDto
{
    public string Key { get; set; } = string.Empty;  // "complete-profile", "first-comment" gibi
    public string Label { get; set; } = string.Empty; // UI'da gösterilecek text
    public int PotentialXp { get; set; }
    public bool Completed { get; set; }
}
