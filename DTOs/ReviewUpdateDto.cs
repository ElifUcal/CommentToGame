using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.DTOs
{
    public class ReviewUpdateDto
    {
         public int? StarCount { get; set; }

        public string? Comment { get; set; }

        // JSON'da "comment": null gelirse explicit set kabul edilmesi için
        public bool CommentSet { get; set; } = false;

        public bool? IsSpoiler { get; set; }
    }
}