using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.DTOs
{
    public class RefreshTokenRequest
    {
        public required string RefreshToken { get; set; }
    }
}