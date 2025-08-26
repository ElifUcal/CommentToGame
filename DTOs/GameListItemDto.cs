// DTOs/GameListItemDto.cs
using System.Text.Json.Serialization;

namespace CommentToGame.DTOs;

public sealed class GameListItemDto
{
    [JsonPropertyName("id")]        public required string Id { get; set; }
    [JsonPropertyName("cover")]     public string? Cover { get; set; }
    [JsonPropertyName("title")]     public required string Title { get; set; }
    [JsonPropertyName("release")]   public DateTime? Release { get; set; }
    [JsonPropertyName("developer")] public string? Developer { get; set; }
    [JsonPropertyName("genres")]    public List<string> Genres { get; set; } = new();

    // YENİ
    [JsonPropertyName("platforms")] public List<string> Platforms { get; set; } = new();

    [JsonPropertyName("story")]     public string? Story { get; set; }
}
