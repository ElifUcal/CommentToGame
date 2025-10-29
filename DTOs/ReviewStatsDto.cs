using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.DTOs
{
    public class ReviewStatsDto
    {
        public string GameId { get; set; } = default!;
        public int Total { get; set; }
        public double Average { get; set; }
        public Dictionary<int, int> Distribution { get; set; } = new();
    }
}