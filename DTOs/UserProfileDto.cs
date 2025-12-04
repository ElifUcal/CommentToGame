using CommentToGame.Models;

namespace CommentToGame.Dtos
{
    public class UserProfileDto
    {
        
    public string Id { get; set; } = default!;

    
    public required string UserName { get; set; }

    
    public required string Email { get; set; }
  
 

    public required DateTime Createdat { get; set; } = DateTime.Now;


    public required DateTime Birthdate { get; set; }


    public required string Country { get; set; }

   
    public string? ProfileImageUrl { get; set; }

    public string? BannerUrl { get; set; }

    public string[]? Platforms { get; set; }

    public string? Badge { get; set; }

    public string[]? Title { get; set; }

    public string? City { get; set; }

    public string[]? ContactUrl { get; set; }

    public string? About { get; set; }

    public string[]? Skills { get; set; }

    public WorkExperience[]? Experiences { get; set; }

    public ProjectItems[]? Projects { get; set; }


    public Educations[]? Educations { get; set; }

    public Awards[]? Awards { get; set; }

    public List<string>? FavoriteGenres { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public List<string>? FavConsoles { get; set; }

    public Equipment? Equipment { get; set; }

    public string? CareerGoal { get; set; }
    }
}


