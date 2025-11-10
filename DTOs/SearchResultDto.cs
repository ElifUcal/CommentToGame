using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommentToGame.Models;

namespace CommentToGame.DTOs;

public class SearchResultDto
{
    public List<GameSearchDto> Games { get; set; } = new();
    public List<UserSearchDto> Users { get; set; } = new();
    public List<GenreSearchDto> Genres { get; set; } = new();
    public List<CompanySearchDto> Companies { get; set; } = new();
    public List<PlatformSearchDto> Platforms { get; set; } = new();
    public List<AwardSearchDto> Awards { get; set; } = new();
}

public class GameSearchDto
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? CoverUrl { get; set; }
    public string? Developer { get; set; }
    public int? Rating { get; set; }
    public string? ReleaseYear { get; set; }
}

public class UserSearchDto
{
    public string Id { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string? ProfileImageUrl { get; set; }
    public string? Country { get; set; }
}

public class GenreSearchDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public class CompanySearchDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public class PlatformSearchDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public class AwardSearchDto
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int? Year { get; set; }
}
