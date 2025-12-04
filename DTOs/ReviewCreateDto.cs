using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.DTOs
{
    public class ReviewCreateDto
    {
        public required string GameId { get; set; }
        public int StarCount { get; set; }
        public string? Comment { get; set; }
        public bool IsSpoiler { get; set; }
    }
}