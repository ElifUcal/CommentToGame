using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommentToGame.Models;           // StoreLink
using CommentToGame.DTOs;            // RAWG DTO'ları
using static CommentToGame.DTOs.IGdbDto;
using System.Text;
using System.Text.RegularExpressions;

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

            public string? GameDirector { get; set; }
            public List<string>? Writers { get; set; }
            public string? ArtDirector { get; set; }
            public List<string>? LeadActors { get; set; }
            public List<string>? VoiceActors { get; set; }
            public string? MusicComposer { get; set; }
            public List<string>? CinematicsVfxTeam { get; set; }

            public ImageDto? FeaturedSectionBackground { get; set; } // = Main Image
            public ImageDto? PosterImage { get; set; }                // = Screenshot 1
            public VideoDto? PosterVideo { get; set; }                // = Trailer 1


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

            // PC gereksinimleri
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
    var minText = BuildReqTemplate(pc.Requirements.Minimum);
    var recText = BuildReqTemplate(pc.Requirements.Recommended);

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
                .Select((t, i) => new VideoDto
                {
                    Url = ToVideoUrl(t),
                    YouTubeId = string.IsNullOrWhiteSpace(t.YouTubeId) ? null : t.YouTubeId,
                    Title = $"Trailer {i + 1}",
                    MetaDatas = new List<MetaData>() // şimdilik boş
                })
                .ToList();

            var mainImageUrl = dto.MainImage;
            if (!string.IsNullOrWhiteSpace(mainImageUrl))
            {
                var alreadyExists = images.Any(i =>
                    string.Equals(i.Url, mainImageUrl, StringComparison.OrdinalIgnoreCase));

                if (!alreadyExists)
                {
                    images.Insert(0, new ImageDto
                    {
                        Url = mainImageUrl,
                        Title = "Main Image",
                        MetaDatas = new List<MetaData> {
                new MetaData { Label = "Type", Value = "Main" }
            }
                    });
                }
                else
                {
                    // Varsa ama sona düşmüşse, başa al ve adını "Main Image" yap
                    var idx = images.FindIndex(i => string.Equals(i.Url, mainImageUrl, StringComparison.OrdinalIgnoreCase));
                    if (idx > 0)
                    {
                        var main = images[idx];
                        main.Title = "Main Image";
                        images.RemoveAt(idx);
                        images.Insert(0, main);
                    }
                    else
                    {
                        // zaten 0. sıradaysa sadece başlığını güncelle
                        images[0].Title = "Main Image";
                    }
                }
            }




            // Yeni alanlara ata
            dto.Images = images;

            dto.Videos = videos;

            dto.GameDirector ??= "";
            dto.Writers ??= new();
            dto.ArtDirector ??= "";
            dto.LeadActors ??= new();
            dto.VoiceActors ??= new();
            dto.MusicComposer ??= "";
            dto.CinematicsVfxTeam ??= new();




            // --- POSTER & FEATURED eşleştirmesi (İSTENEN: Poster = Main Image, Featured = Screenshot 1) ---

            // 0) Mevcut "Main Image" (ekleme kısmında zaten 0. sıraya yerleştiriyoruz ve başlığı "Main Image")
            var mainImgDto =
                images.FirstOrDefault(i => string.Equals(i.Title, "Main Image", StringComparison.OrdinalIgnoreCase))
                // (ekstra emniyet) başlık bulunamazsa URL ile yakalamayı dene
                ?? (string.IsNullOrWhiteSpace(mainImageUrl)
                    ? null
                    : images.FirstOrDefault(i => string.Equals(i.Url, mainImageUrl, StringComparison.OrdinalIgnoreCase)))
                // yine de yoksa ilk görsel
                ?? images.FirstOrDefault();

            // 1) "Screenshot 1" görseli
            var ss1Dto = images.FirstOrDefault(i =>
                !string.IsNullOrWhiteSpace(i.Title) &&
                i.Title.StartsWith("Screenshot 1", StringComparison.OrdinalIgnoreCase));

            // 1.a) Screenshot 1 yoksa → main olmayan ilk görsel (varsa)
            if (ss1Dto == null)
            {
                ss1Dto = images.FirstOrDefault(i => !object.ReferenceEquals(i, mainImgDto))
                         ?? images.Skip(mainImgDto != null ? 1 : 0).FirstOrDefault();
            }

            // === Nihai atamalar ===
            // Poster = Main Image
            dto.PosterImage = mainImgDto ?? ss1Dto; // hiçbir şey yoksa ss1’e düş

            // Featured = Screenshot 1 (yoksa main olmayan ilk; o da yoksa main)
            dto.FeaturedSectionBackground = ss1Dto ?? mainImgDto ?? images.FirstOrDefault();

            // Video tara: "Trailer 1" → yoksa ilk video
            var tr1Dto = videos.FirstOrDefault(v =>
                string.Equals(v.Title, "Trailer 1", StringComparison.OrdinalIgnoreCase))
                ?? videos.FirstOrDefault();

            dto.PosterVideo = tr1Dto;






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


        // -- REQ HELPERS

        private static string TrimTrailingLegalese(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return "";

    // Legal / EULA / kurulum vs. metinlerini kesmek için tetikleyiciler
    var cutRx = new Regex(
        @"\b(Over\s+time|Installation\b|SINGLE\s+USE\b|Please\s+refer\b|Some\s+system\s+components\b|Unlisted\s+specifications\b|Partner\s+Requirements\b|Other\s+requirements\b|online\s+play\s+requires\b|terms\s+of\s+service\b)\b",
        RegexOptions.IgnoreCase
    );

    var m = cutRx.Match(value);
    if (m.Success)
    {
        value = value.Substring(0, m.Index);
    }

    // URL’leri ve gereksiz iç içe boşlukları sadeleştir
    value = Regex.Replace(value, @"https?://\S+", ""); // URL sil
    value = Regex.Replace(value, @"\s+", " ").Trim();  // whitespace normalize

    // Sonda noktalama/bağlaç kalmışsa temizle
    value = Regex.Replace(value, @"[;\.,:\-–—]+\s*$", "").Trim();

    return value;
}


        private static readonly Regex DROP_KEYS = new(
    @"^(additional\s*notes|other\s*requirements|partner\s*requirements)$",
    RegexOptions.IgnoreCase
);


        private static readonly (Regex rx, string key)[] REQ_ALIASES = new[]
    {
    (new Regex(@"^(os|operating\s*system)$", RegexOptions.IgnoreCase), "OS"),
    (new Regex(@"^(cpu|processor)$",          RegexOptions.IgnoreCase), "CPU"),
    (new Regex(@"^(gpu|graphics|graphics\s*card|video\s*card)$", RegexOptions.IgnoreCase), "GPU"),
    (new Regex(@"^(ram|memory)$",             RegexOptions.IgnoreCase), "RAM"),
    (new Regex(@"^(storage|disk|hdd|ssd)$",   RegexOptions.IgnoreCase), "Storage"),
    (new Regex(@"^direct\s*x$",               RegexOptions.IgnoreCase), "DirectX"),
    (new Regex(@"^(network|internet)$",       RegexOptions.IgnoreCase), "Network"),
    (new Regex(@"^(sound|sound\s*card|audio)$", RegexOptions.IgnoreCase), "Sound"),
};

        private static readonly string[] REQ_ORDER = new[]
        {
    "OS","CPU","RAM","GPU","Storage","DirectX","Network","Sound","Other"
};

        private static string NormalizeReqKey(string k)
        {
            var key = (k ?? "").Trim();
            key = Regex.Replace(key, @"\s+", " ");
            foreach (var (rx, target) in REQ_ALIASES)
                if (rx.IsMatch(key)) return target;
            // Title-case ilk harfi büyük
            if (key.Length == 0) return "Other";
            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        private static string InjectBreaksBeforeKeys(string s)
{
    // OS:, Operating System:, Processor:/CPU:, Memory/RAM:, Graphics/GPU:, Storage:, Sound/Sound Card/Audio:, DirectX/Direct X:
    var keyAlt = @"OS|Operating\s*System|Processor|CPU|Memory|RAM|Graphics(?:\s*Card)?|GPU|Storage|Sound(?:\s*Card)?|Audio|Direct\s*X|DirectX";

    // Başta değilse ve bu anahtarlardan biri geliyorsa öncesine \n ekle
    // Örn: "..., Windows 10)Processor: Intel..."  ->  "...\nProcessor: Intel..."
    s = Regex.Replace(
        s,
        $@"(?<!^)\s*(?=(?:{keyAlt})\s*:)",
        "\n",
        RegexOptions.IgnoreCase
    );

    return s;
}


       private static string CleanText(string? t)
{
    if (string.IsNullOrWhiteSpace(t)) return "";
    var s = t.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
             .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
             .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);
    s = Regex.Replace(s, "<.*?>", string.Empty);
    s = s.Replace("\r\n", "\n").Replace('\r', '\n');

    // "Minimum:" / "Recommended:" başlığını tamamen kaldır (ham metinde en başta oluyor)
    s = Regex.Replace(s, @"^\s*(minimum|recommended)\s*:\s*", "", RegexOptions.IgnoreCase);

    s = InjectBreaksBeforeKeys(s);   // 🔴 kritik adım
    return s.Trim();
}

    private static Dictionary<string, List<string>> ParseReqBlock(string? text)
        {
     string key = default!;
    string val = default!;
    var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    var src = CleanText(text);
    if (string.IsNullOrWhiteSpace(src)) return dict;

    // Baştaki "Minimum:" / "Recommended:" kaldır
    src = Regex.Replace(src, @"^\s*(minimum|recommended)\s*:\s*", "", RegexOptions.IgnoreCase);

    // Gürültü başlıklarını komple çıkar (opsiyonel ama faydalı)
    src = Regex.Replace(src,
        @"(additional\s*notes|other\s*requirements|partner\s*requirements)\s*:\s*",
        "", RegexOptions.IgnoreCase);

    // Anahtar listesi (aliaslar)
    const string KeyAlt =
        @"OS|Operating\s*System|Processor|CPU|Memory|RAM|Graphics(?:\s*Card)?|GPU|Storage|Sound(?:\s*Card)?|Audio|Direct\s*X|DirectX";

    // Esas eşleşme: "Key: Value" ... "Key: Value" ... şeklinde ardışık yakala
    // Grup1: Key   Grup2: Value (bir sonraki Key'e kadar her şey)
    var rx = new Regex(
        $@"(?:(?<{nameof(key)}>{KeyAlt})\s*:\s*)(?<{nameof(val)}>.*?)(?=(?:{KeyAlt})\s*:|$)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline
    );

    var matches = rx.Matches(src);

    foreach (Match m in matches)
    {
        var rawKey = m.Groups[nameof(key)].Value;
        var value  = m.Groups[nameof(val)].Value?.Trim();

        if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(value))
            continue;

        var k = NormalizeReqKey(rawKey);

        if (!dict.TryGetValue(k, out var list))
            dict[k] = list = new List<string>();
                value = TrimTrailingLegalese(value);
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
    list.Add(value);
        // Değerleri biraz temizle: gereksiz boşluklar, art arda virgüller vs.
        value = Regex.Replace(value, @"\s+", " ").Trim();
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);
    }

    // Eğer hiç eşleşme bulunamadıysa (RAWG bazen tamamen serbest metin dökebilir)
    if (dict.Count == 0)
    {
        dict["Other"] = new List<string> { src.Trim() };
    }

    return dict;

    // local to avoid naming warnings
   
}


        private static string BuildReqTemplate(string? text)
        {
            var dict = ParseReqBlock(text);
            if (dict.Count == 0) return "";

            // belirlediğimiz sırada yaz
            var sb = new StringBuilder();
            foreach (var key in REQ_ORDER)
            {
                if (!dict.TryGetValue(key, out var values) || values.Count == 0) continue;
                var joined = string.Join(" / ", values);
                sb.Append(key).Append(": ").Append(joined).Append('\n');
            }

            // sırada olmayan anahtarlar kaldıysa, en sona ekle
            var restKeys = dict.Keys
                .Where(k => !REQ_ORDER.Contains(k, StringComparer.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

            foreach (var k in restKeys)
            {
                var joined = string.Join(" / ", dict[k]);
                sb.Append(k).Append(": ").Append(joined).Append('\n');
            }

            return sb.ToString().TrimEnd(); // sonda \n olmasın
        }

    }
}

