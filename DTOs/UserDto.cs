using CommentToGame.Models;

namespace CommentToGame.Dtos
{
    public class UserDto
    {
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }

        public required DateTime Birthdate { get; set; }
        public required string Country { get; set; }

        public string? ProfileImageUrl { get; set; }
        public required UserType userType { get; set; }
        
    }
}
