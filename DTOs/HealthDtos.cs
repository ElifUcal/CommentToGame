using System;

namespace CommentToGame.DTOs
{
    public enum HealthState { Healthy, Degraded, Down }

    public sealed class HealthItemDto
    {
        public string Title { get; set; } = "";
        public HealthState Status { get; set; } = HealthState.Healthy;
        public string Color { get; set; } = "#22C55E";
        public long? LatencyMs { get; set; }
        public string? Detail { get; set; }
    }

    public sealed class HealthResponseDto
    {
        public DateTime Utc { get; set; }
        public System.Collections.Generic.List<HealthItemDto> Items { get; set; } = new();
    }
}
