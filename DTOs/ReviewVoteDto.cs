// DTOs/ReviewVoteDto.cs
namespace CommentToGame.DTOs
{
    public class ReviewVoteDto
    {
        public  string? UserId { get; set; }
        
        public required int Value { get; set; }
    }
}
