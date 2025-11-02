// DTOs/ReviewVoteDto.cs
namespace CommentToGame.DTOs
{
    public class ReviewVoteDto
    {
        public required string UserId { get; set; }
        /// <summary> 1 (like) veya -1 (dislike) </summary>
        public required int Value { get; set; }
    }
}
