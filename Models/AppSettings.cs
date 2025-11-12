// Models/AppSettings.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class AppSettings
{
    [BsonId] public string Id { get; set; } = "app_settings"; // tek kayıt
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }

    public GeneralSettings General { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public ContentSettings Content { get; set; } = new();
    public SeoSettings Seo { get; set; } = new();
}

public class GeneralSettings
{
    public string SiteName { get; set; } = "GGDB";
    public string SiteDesc { get; set; } = "The Ultimate Gaming Database";
    public bool Maintenance { get; set; } = true;
    public bool AllowRegistrations { get; set; } = false;
    public string? FaviconUrl { get; set; } // binary değil: URL
}

public class SecuritySettings { public bool TwoFA { get; set; } = true; public int Timeout { get; set; } = 30; public int MaxAttempts { get; set; } = 5; public int MinLength { get; set; } = 8; }
public class ContentSettings { public bool AutoMod { get; set; } = true; public bool RequireApproval { get; set; } = true; public bool AllowReviews { get; set; } = true; public int MaxFileMb { get; set; } = 10; }

public class SeoSettings
{
    public string TitleTpl { get; set; } = "%s | GGDB - Gaming Database";
    public string MetaDesc { get; set; } = "Discover, rate, and review the best games...";
    public string Keywords { get; set; } = "gaming, games, database, reviews, ratings";
    public string Canonical { get; set; } = "https://ggdb.com";
    public string OgImage { get; set; } = "https://ggdb.com/og-image.jpg";
    public string TwitterType { get; set; } = "summary_large_image";
    public string TwitterHandle { get; set; } = "@GGDB";
    public bool GenSitemap { get; set; } = true;
    public bool JsonLd { get; set; } = true;
    public bool AnalyticsEnabled { get; set; } = false;
    public string AnalyticsId { get; set; } = "";
}
