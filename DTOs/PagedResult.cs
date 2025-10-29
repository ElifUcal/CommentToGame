using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.DTOs
{
    public class PagedResult<T>
    {
           public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<T> Items { get; set; } = new();
    }
}