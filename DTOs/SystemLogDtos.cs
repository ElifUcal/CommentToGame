namespace CommentToGame.DTOs;

public class LogQuery
{
    public string? Q { get; set; }                 // search
    public string[]? Levels { get; set; }          // ["Error","Info",...]
    public string[]? Categories { get; set; }      // ["Auth","User",...]
    public DateTime? From { get; set; }            // ISO (UTC)
    public DateTime? To { get; set; }              // ISO (UTC)

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
public class Paged<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
