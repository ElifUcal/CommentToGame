using CommentToGame.Models;

namespace CommentToGame.Dtos
{
    public class UserDto
    {

        public string? Id { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }

        public required DateTime Birthdate { get; set; }
        public required string Country { get; set; }

        public string? ProfileImageUrl { get; set; }
        public  UserType? UserType { get; set; }
        
    }
}
