// Services/Merge/MergedGameDto.cs
using System;
using System.Collections.Generic;
using CommentToGame.Models; // StoreLink için

namespace CommentToGame.DTOs;

public sealed class MergedGameDto
{
    // Game
    public string? Name { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public int? Metacritic { get; set; }
    public int? GgDbRating { get; set; }
    public string? MainImage { get; set; }
    public int? Popularity { get; set; } // RAWG'dan geliyorsa int/nullable olabiliyor

    // Details
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public string? About { get; set; }

    public List<string> AgeRatings { get; set; } = new();
    public List<string> Dlcs { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> Genres { get; set; } = new();
    public List<string> Platforms { get; set; } = new();

    public string? MinRequirement { get; set; }
    public string? RecRequirement { get; set; }

    public List<string> AudioLanguages { get; set; } = new();
    public List<string> Subtitles { get; set; } = new();
    public List<string> InterfaceLanguages { get; set; } = new();
    public List<string> ContentWarnings { get; set; } = new();

    public List<StoreLink> StoreLinks { get; set; } = new();

    // TTB (saat cinsinden; IGDB saniyeyi verdiği için dönüştürürüz)
    public int? TimeToBeatHastily { get; set; }
    public int? TimeToBeatNormally { get; set; }
    public int? TimeToBeatCompletely { get; set; }

    public List<string> Engines { get; set; } = new();
    public List<string> Awards { get; set; } = new();

    // Cast/Crew (RAWG’a göre)
    public List<string> Cast { get; set; } = new();
    public List<string> Crew { get; set; } = new();
}
