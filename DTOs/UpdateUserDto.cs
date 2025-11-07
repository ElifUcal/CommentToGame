using CommentToGame.Models;

public class UpdateUserDto
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public DateTime? Birthdate { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? About { get; set; }
    public string? FavoriteGenres { get; set; }

    public string[]? Platforms { get; set; }
    public string? Badge { get; set; }
    public string[]? Title { get; set; }
    public string[]? ContactUrl { get; set; }
    public string[]? Skills { get; set; }

    public WorkExperience[]? Experiences { get; set; }
    public ProjectItems[]? Projects { get; set; }
    public Educations[]? Educations { get; set; }
    public Awards[]? Awards { get; set; }

    // Sadece Admin değiştirebilsin
    
}
