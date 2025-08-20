// Services/GameMerge.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommentToGame.Models;           // StoreLink
using CommentToGame.DTOs;            // RAWG DTO'ları
using static CommentToGame.DTOs.IGdbDto;

namespace CommentToGame.Services
{
    public static class GameMerge
    {
        public sealed class TimeToBeatDto
        {
            public int? Hastily { get; set; }
            public int? Normally { get; set; }
            public int? Completely { get; set; }
        }

        public sealed class MergedGameDto
        {
            public string? Name { get; set; }
            public DateTime? ReleaseDate { get; set; }
            public int? Metacritic { get; set; }
            public int? GgDbRating { get; set; }
            public string? MainImage { get; set; }
            public int? Popularity { get; set; }
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

            public TimeToBeatDto TimeToBeat { get; set; } = new();
            public List<string> Engines { get; set; } = new();
            public List<string> Awards { get; set; } = new();

            public List<string> Cast { get; set; } = new();
            public List<string> Crew { get; set; } = new();
        }

        public static MergedGameDto Merge(
            IgdbGameDetail? igdb,
            RawgGameDetail? rawg,
            IgdbTimeToBeat? igdbTtbSeconds,
            List<StoreLink>? storeLinks = null,
            IEnumerable<string>? rawgCast = null,
            IEnumerable<string>? rawgCrew = null,
            IEnumerable<string>? igdbDlcs = null)   // IGDB additions’tan gelen DLC isimleri
        {
            var dto = new MergedGameDto();

            // name: IGDB = RAWG
            dto.Name = FirstNonEmpty(igdb?.Name, rawg?.Name);

            // releaseDate: IGDB > RAWG.Released
            dto.ReleaseDate = igdb?.ReleaseDate ?? ParseDate(rawg?.Released);

            // metacritic: RAWG
            dto.Metacritic = rawg?.Metacritic;

            // ggDbRating: RAWG.rating (0–5) -> 0–100
            dto.GgDbRating = rawg?.Rating is double r ? (int?)Math.Round(r * 20) : null;

            // mainImage: IGDB > RAWG
            dto.MainImage = FirstNonEmpty(igdb?.BackgroundImage, rawg?.BackgroundImage);

            // popularity: RAWG.added
            dto.Popularity = rawg?.Added;

            // developer/publisher: IGDB > RAWG
            dto.Developer = FirstNonEmpty(
                igdb?.Developers?.FirstOrDefault(),
                rawg?.Developers?.FirstOrDefault()?.Name
            );
            dto.Publisher = FirstNonEmpty(
                igdb?.Publishers?.FirstOrDefault(),
                rawg?.Publishers?.FirstOrDefault()?.Name
            );

            // about: IGDB.summary > RAWG.description_raw
            dto.About = FirstNonEmpty(igdb?.Summary, rawg?.DescriptionRaw);

            // ageRatings: IGDB > RAWG (ESRB + list)
            dto.AgeRatings = (igdb?.AgeRatings ?? Enumerable.Empty<string>())
                .WhereNotEmpty().Distinct().ToList();
            if (dto.AgeRatings.Count == 0)
                dto.AgeRatings = BuildRawgAgeRatings(rawg);

            // dlcs: IGDB additions’tan gelenler
            dto.Dlcs = igdbDlcs?.WhereNotEmpty().Distinct().ToList() ?? new List<string>();

            // tags: IGDB > RAWG
            dto.Tags = (igdb?.Tags ?? Enumerable.Empty<string>())
                .WhereNotEmpty().Distinct().ToList();
            if (dto.Tags.Count == 0 && rawg?.Tags != null)
                dto.Tags = rawg.Tags.Select(t => t.Name).WhereNotEmpty().Distinct().ToList();

            // genres: IGDB > RAWG
            dto.Genres = (igdb?.Genres ?? Enumerable.Empty<string>())
                .WhereNotEmpty().Distinct().ToList();
            if (dto.Genres.Count == 0 && rawg?.Genres != null)
                dto.Genres = rawg.Genres.Select(g => g.Name).WhereNotEmpty().Distinct().ToList();

            // platforms: IGDB > RAWG
            dto.Platforms = (igdb?.Platforms ?? Enumerable.Empty<string>())
                .WhereNotEmpty().Distinct().ToList();
            if (dto.Platforms.Count == 0 && rawg?.Platforms != null)
                dto.Platforms = rawg.Platforms.Select(p => p.Platform?.Name).WhereNotEmpty().Distinct().ToList();

            // requirements (PC)
            if (rawg?.Platforms != null)
            {
                var pc = rawg.Platforms.FirstOrDefault(p =>
                    (p.Platform?.Name ?? "").Contains("PC", StringComparison.OrdinalIgnoreCase) ||
                    (p.Platform?.Slug ?? "").Contains("pc", StringComparison.OrdinalIgnoreCase));

                dto.MinRequirement = pc?.Requirements?.Minimum;
                dto.RecRequirement = pc?.Requirements?.Recommended;
            }

            // languages & content warnings: IGDB
            dto.AudioLanguages     = (igdb?.AudioLanguages     ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.Subtitles          = (igdb?.Subtitles          ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.InterfaceLanguages = (igdb?.InterfaceLanguages ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.ContentWarnings    = (igdb?.ContentWarnings    ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();

            // storeLinks: RAWG (controller’dan hazır liste geliyor)
            dto.StoreLinks = storeLinks ?? new List<StoreLink>();

            // time to beat: IGDB (saniye -> saat, yakınsama ile)
            if (igdbTtbSeconds != null)
            {
                dto.TimeToBeat = new TimeToBeatDto
                {
                    Hastily     = ToHours(igdbTtbSeconds.Hastily),
                    Normally    = ToHours(igdbTtbSeconds.Normally),
                    Completely  = ToHours(igdbTtbSeconds.Completely)
                };
            }

            // engines: IGDB
            dto.Engines = (igdb?.Engines ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();

            // awards: IGDB
            dto.Awards = (igdb?.Awards ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();

            // cast/crew: RAWG’den parametre ile gelebilir
            if (rawgCast != null) dto.Cast = rawgCast.WhereNotEmpty().Distinct().ToList();
            if (rawgCrew != null) dto.Crew = rawgCrew.WhereNotEmpty().Distinct().ToList();

            return dto;
        }

        // ---------- helpers ----------

        private static int? ToHours(int? seconds)
            => seconds.HasValue ? (int?)Math.Round(seconds.Value / 3600.0) : null;

        private static DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
                return dt;
            return null;
        }

        private static string? FirstNonEmpty(params string?[] items)
            => items.Select(s => s?.Trim()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        private static List<string> BuildRawgAgeRatings(RawgGameDetail? rawg)
        {
            var list = new List<string>();
            if (rawg == null) return list;

            if (!string.IsNullOrWhiteSpace(rawg.EsrbRating?.Name))
                list.Add($"ESRB {rawg.EsrbRating.Name}");

            if (rawg.AgeRatings != null && rawg.AgeRatings.Count > 0)
                list.AddRange(rawg.AgeRatings.Select(a => a.Name).WhereNotEmpty());

            return list.Distinct().ToList();
        }

        private static IEnumerable<string> WhereNotEmpty(this IEnumerable<string?> src)
            => src.Where(s => !string.IsNullOrWhiteSpace(s))!.Select(s => s!.Trim());
    }
}
