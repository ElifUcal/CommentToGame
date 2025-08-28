namespace CommentToGame.DTOs;

public class GameDetailDto
{
    public string Id { get; set; } = default!;
    
    // Game (temel)
    public string Title { get; set; } = default!;
    public DateTime? ReleaseDate { get; set; }
    public string? Studio { get; set; }
    public int? GgdbRating { get; set; }
    public int? MetacriticRating { get; set; }
    public string? Cover { get; set; }
    public string? Video { get; set; }

    // Game_Details (detaylar)
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Platforms { get; set; } = new();
    public string? Story { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Dlcs { get; set; } = new();
    public List<string> Crew { get; set; } = new();
    public List<string>? Awards { get; set; }
    public List<string> GameEngine { get; set; } = new();
}
