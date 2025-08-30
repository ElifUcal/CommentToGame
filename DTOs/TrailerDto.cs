namespace CommentToGame.DTOs
{
    public sealed class TrailerDto
    {
        public string? Platform  { get; set; } // "youtube" | "rawg" | "other"
        public string? Url       { get; set; } // tam video linki (YouTube/watch?v=... veya mp4)
        public string? YouTubeId { get; set; } // varsa
    }
}
