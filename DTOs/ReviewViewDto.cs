// DTOs/ReviewViewDto.cs
namespace CommentToGame.DTOs
{
    public class ReviewViewDto
    {
        public string? Id { get; set; } = default!;
        public string GameId { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public int StarCount { get; set; }
        public string? Comment { get; set; }
        public bool IsSpoiler { get; set; }
        public DateTime TodayDate { get; set; }

        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }

        public int MyVote { get; set; }

        public int ReplyCount { get; set; }
    }
}
