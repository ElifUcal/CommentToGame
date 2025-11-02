// DTOs/ReviewReplyDtos.cs
namespace CommentToGame.DTOs
{
    public class ReviewReplyCreateDto
    {
        public required string UserId { get; set; }
        public required string Comment { get; set; }
        public bool IsSpoiler { get; set; }
    }

    public class ReviewReplyDto
    {
        public string Id { get; set; } = default!;
        public string ReviewId { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string Comment { get; set; } = default!;
        public bool IsSpoiler { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
