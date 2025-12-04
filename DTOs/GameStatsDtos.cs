namespace CommentToGame.Dtos;

public class GenreStatDto
{
    public string Name { get; set; } = default!;
    public double Percent { get; set; }   // 0–100
    public int Count { get; set; }        // kaç kere sayıldı
}

public class UserGamingStatsDto
{
    public double CompletionRate { get; set; }  // 0–100
    public int TotalTrackedGames { get; set; }  // progress’i olan oyun sayısı

    public List<GenreStatDto> Genres { get; set; } = new();
}
