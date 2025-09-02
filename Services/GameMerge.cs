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
            public int? Hastily { get; set; }     // saat cinsinden
            public int? Normally { get; set; }    // saat cinsinden
            public int? Completely { get; set; }  // saat cinsinden
        }

        // minRequirement / recRequirement için esnek giriş:
        // { "id": "66c...", "text": "Minimum: ..." } veya { "text": "Minimum: ..." }
        public sealed class RequirementInput
        {
            public string? Id { get; set; }
            public string? Text { get; set; }
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
            

            // ARTIK obje bekliyoruz (id ve/veya text)
            public RequirementInput? MinRequirement { get; set; }
            public RequirementInput? RecRequirement { get; set; }

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
            public DateTime Createdat { get; set; } = DateTime.Now;

            public List<string> Screenshots { get; set; } = new();
            public List<TrailerDto> Trailers { get; set; } = new();
            
             public List<ImageDto> Images { get; set; } = new();
            public List<VideoDto> Videos { get; set; } = new();
        }

        public sealed class ImageDto
        {
            public required string Url { get; set; }
            public required string Title { get; set; }
            public List<MetaData>? MetaDatas { get; set; }  // şimdilik boş kalabilir
        }

        public sealed class VideoDto
        {
            public required string Url { get; set; }        // YouTube id varsa buraya kısa url de vereceğiz
            public required string Title { get; set; }      // "Trailer 1" gibi
            public string? YouTubeId { get; set; }          // opsiyonel
            public List<MetaData>? MetaDatas { get; set; }  // şimdilik boş
        }


        public static MergedGameDto Merge(
            IgdbGameDetail? igdb,
            RawgGameDetail? rawg,
            IgdbTimeToBeat? igdbTtbSeconds,
            List<StoreLink>? storeLinks = null,
            IEnumerable<string>? rawgCast = null,
            IEnumerable<string>? rawgCrew = null,
            IEnumerable<string>? igdbDlcs = null,
            IEnumerable<string>? igdbScreenshots = null,
            IEnumerable<TrailerDto>? igdbTrailers = null
            )
        {
            var dto = new MergedGameDto();

            dto.Name = FirstNonEmpty(igdb?.Name, rawg?.Name);
            dto.ReleaseDate = igdb?.ReleaseDate ?? ParseDate(rawg?.Released);
            dto.Metacritic = rawg?.Metacritic;
            dto.GgDbRating = rawg?.Rating is double r ? (int?)Math.Round(r * 20) : null;
            dto.MainImage = FirstNonEmpty(igdb?.BackgroundImage, rawg?.BackgroundImage);
            dto.Popularity = rawg?.Added;

            dto.Developer = FirstNonEmpty(igdb?.Developers?.FirstOrDefault(), rawg?.Developers?.FirstOrDefault()?.Name);
            dto.Publisher = FirstNonEmpty(igdb?.Publishers?.FirstOrDefault(), rawg?.Publishers?.FirstOrDefault()?.Name);
            dto.About = FirstNonEmpty(igdb?.Summary, rawg?.DescriptionRaw);

            dto.AgeRatings = (igdb?.AgeRatings ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            if (dto.AgeRatings.Count == 0)
                dto.AgeRatings = BuildRawgAgeRatings(rawg);

            dto.Dlcs = igdbDlcs?.WhereNotEmpty().Distinct().ToList() ?? new List<string>();

            dto.Tags = (igdb?.Tags ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            if (dto.Tags.Count == 0 && rawg?.Tags != null)
                dto.Tags = rawg.Tags.Select(t => t.Name).WhereNotEmpty().Distinct().ToList();

            dto.Genres = (igdb?.Genres ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            if (dto.Genres.Count == 0 && rawg?.Genres != null)
                dto.Genres = rawg.Genres.Select(g => g.Name).WhereNotEmpty().Distinct().ToList();

            dto.Platforms = (igdb?.Platforms ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            if (dto.Platforms.Count == 0 && rawg?.Platforms != null)
                dto.Platforms = rawg.Platforms.Select(p => p.Platform?.Name).WhereNotEmpty().Distinct().ToList();

            // PC gereksinimleri → RequirementInput.Text doldur
            var pc = rawg?.Platforms?.FirstOrDefault(p =>
            {
                var name = p.Platform?.Name;
                var slug = p.Platform?.Slug;
                var hasPcInName = !string.IsNullOrEmpty(name) && name.IndexOf("PC", StringComparison.OrdinalIgnoreCase) >= 0;
                var hasPcInSlug = !string.IsNullOrEmpty(slug) && slug.IndexOf("pc", StringComparison.OrdinalIgnoreCase) >= 0;
                return hasPcInName || hasPcInSlug;
            });

            if (pc?.Requirements != null)
            {
                var minText = pc.Requirements.Minimum;
                var recText = pc.Requirements.Recommended;

                if (!string.IsNullOrWhiteSpace(minText))
                    dto.MinRequirement = new RequirementInput { Text = minText };

                if (!string.IsNullOrWhiteSpace(recText))
                    dto.RecRequirement = new RequirementInput { Text = recText };
            }

            dto.AudioLanguages = (igdb?.AudioLanguages ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.Subtitles = (igdb?.Subtitles ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.InterfaceLanguages = (igdb?.InterfaceLanguages ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.ContentWarnings = (igdb?.ContentWarnings ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();

            dto.StoreLinks = storeLinks ?? new List<StoreLink>();

            if (igdbTtbSeconds != null)
            {
                dto.TimeToBeat = new TimeToBeatDto
                {
                    Hastily = ToHours(igdbTtbSeconds.Hastily),
                    Normally = ToHours(igdbTtbSeconds.Normally),
                    Completely = ToHours(igdbTtbSeconds.Completely)
                };
            }

            dto.Engines = (igdb?.Engines ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();
            dto.Awards = (igdb?.Awards ?? Enumerable.Empty<string>()).WhereNotEmpty().Distinct().ToList();

            if (rawgCast != null) dto.Cast = rawgCast.WhereNotEmpty().Distinct().ToList();
            if (rawgCrew != null) dto.Crew = rawgCrew.WhereNotEmpty().Distinct().ToList();

            // --- IMAGES: Screenshot 1, 2, ... başlıklarıyla doldur
            var images = (igdbScreenshots ?? Enumerable.Empty<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select((u, i) => new ImageDto
                {
                    Url = u,
                    Title = $"Screenshot {i + 1}",
                    MetaDatas = new List<MetaData>() // şimdilik boş
                })
                .ToList();

            // --- VIDEOS: Trailer 1, 2, ...; YouTube id varsa kısa url üret
            string ToVideoUrl(TrailerDto t)
            {
                if (!string.IsNullOrWhiteSpace(t.YouTubeId))
                    return $"https://youtu.be/{t.YouTubeId}";
                return t.Url ?? "";
            }

            var videos = (igdbTrailers ?? Enumerable.Empty<TrailerDto>())
                .Where(t => !string.IsNullOrWhiteSpace(t?.Url) || !string.IsNullOrWhiteSpace(t?.YouTubeId))
                .GroupBy(t => string.IsNullOrWhiteSpace(t!.YouTubeId) ? ("url:" + t!.Url) : ("yt:" + t!.YouTubeId),
                        StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(6)
                .Select((t, i) => new VideoDto
                {
                    Url = ToVideoUrl(t),
                    YouTubeId = string.IsNullOrWhiteSpace(t.YouTubeId) ? null : t.YouTubeId,
                    Title = $"Trailer {i + 1}",
                    MetaDatas = new List<MetaData>() // şimdilik boş
                })
                .ToList();
                


            // Yeni alanlara ata
            dto.Images = images;

            dto.Videos = videos;


            dto.Createdat = DateTime.Now;

            return dto;
        }

        // ---------- helpers ----------

        private static List<string> MergeScreens(IEnumerable<string>? a, IEnumerable<string>? b, int take = 12)
{
    var res = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void add(IEnumerable<string>? src)
    {
        if (src == null) return;
        foreach (var u in src)
        {
            var v = (u ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (seen.Add(v))
            {
                res.Add(v);
                if (res.Count >= take) break;
            }
        }
    }
    add(a); add(b);
    return res;
}

private static List<TrailerDto> MergeTrailers(IEnumerable<TrailerDto>? a, IEnumerable<TrailerDto>? b, int take = 6)
{
    var res = new List<TrailerDto>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    IEnumerable<TrailerDto> order(IEnumerable<TrailerDto>? src)
        => (src ?? Enumerable.Empty<TrailerDto>())
           .OrderByDescending(t => string.Equals(t?.Platform, "youtube", StringComparison.OrdinalIgnoreCase));

    foreach (var t in order(a).Concat(order(b)))
    {
        if (t == null) continue;
        var url = (t.Url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url)) continue;
        var key = !string.IsNullOrWhiteSpace(t.YouTubeId) ? $"yt:{t.YouTubeId}" : $"url:{url}";
        if (seen.Add(key))
        {
            res.Add(new TrailerDto { Platform = t.Platform, Url = url, YouTubeId = t.YouTubeId });
            if (res.Count >= take) break;
        }
    }
    return res;
}

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
