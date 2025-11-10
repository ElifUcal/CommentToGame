using CommentToGame.Models;
using CommentToGame.Dtos;
using MongoDB.Driver;
using MongoDB.Bson;
using CommentToGame.DTOs;
using CommentToGame.Data;
using System.Text.RegularExpressions;

public class SearchService
{
    private readonly IMongoCollection<Game> _games;
    private readonly IMongoCollection<Game_Details> _details;
    private readonly IMongoCollection<User> _users;
    private readonly ILogger<SearchService> _logger;

    public SearchService(MongoDbService db, ILogger<SearchService> logger)
    {
        _games = db.GetCollection<Game>("Games");
        _details = db.GetCollection<Game_Details>("GameDetails");
        _users = db.GetCollection<User>("User");
        _logger = logger;
    }

    public async Task<SearchResultDto> GlobalSearchAsync(string query, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(query))
        return new SearchResultDto();

    try
    {
        var result = new SearchResultDto();
        var normalizedQuery = query.Trim();
        var regex = new BsonRegularExpression(normalizedQuery, "i");

        // ✅ 1️⃣ TEXT search (relevance sıralı)
        List<Game> games = new();
        try
        {
            var textFilter = Builders<Game>.Filter.Text(normalizedQuery);
            var projection = Builders<Game>.Projection.MetaTextScore("score");

            var textResults = await _games
                .Find(textFilter)
                .Project<Game>(projection)
                .Sort(Builders<Game>.Sort.MetaTextScore("score"))
                .Limit(10)
                .ToListAsync(ct);

            games.AddRange(textResults);
        }
        catch
        {
            // bazen boş index hatası atar, yoksay
        }

        // ✅ 2️⃣ Eğer text boşsa veya azsa => REGEX fallback (prefix destekli)
        if (games.Count < 5)
        {
            var words = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var regexFilters = new List<FilterDefinition<Game>>();

            foreach (var word in words)
            {
                // 🎯 başta başlayan ve içinde geçen versiyon birlikte
                var startsWith = $"^{Regex.Escape(word)}";
                var wordRegex = new BsonRegularExpression(startsWith, "i");
                var containsRegex = new BsonRegularExpression(word, "i");

                regexFilters.Add(Builders<Game>.Filter.Or(
                    Builders<Game>.Filter.Regex("Game_Name", wordRegex),
                    Builders<Game>.Filter.Regex("Game_Name", containsRegex),
                    Builders<Game>.Filter.Regex("Studio", containsRegex),
                    Builders<Game>.Filter.Regex("Dictionary", containsRegex)
                ));
            }

            var regexFilter = Builders<Game>.Filter.Or(regexFilters);
            var regexResults = await _games.Find(regexFilter).Limit(15).ToListAsync(ct);

            foreach (var r in regexResults)
                if (!games.Any(g => g.Id == r.Id))
                    games.Add(r);
        }

        // ✅ 3️⃣ GameDetails (developer, tags, vs.)
        var gameIds = games.Select(g => g.Id).ToList();
        var details = await _details
            .Find(d => gameIds.Contains(d.GameId))
            .ToListAsync(ct);

        // ✅ 4️⃣ Users
        var userFilter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Regex("userName", regex),
            Builders<User>.Filter.Regex("country", regex)
        );
        var users = await _users.Find(userFilter).Limit(10).ToListAsync(ct);

        // ✅ 5️⃣ DTO dönüşümleri
        result.Games = games
            .Select(g =>
            {
                var detail = details.FirstOrDefault(d => d.GameId == g.Id);
                return new GameSearchDto
                {
                    Id = g.Id,
                    Title = g.Game_Name,
                    CoverUrl = g.Main_image_URL,
                    Developer = !string.IsNullOrEmpty(g.Studio)
                        ? g.Studio
                        : detail?.Developer ?? "Unknown Developer",
                    Rating = g.GgDb_Rating,
                    ReleaseYear = g.Release_Date?.Year.ToString()
                };
            })
            .OrderByDescending(g => g.Title.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ThenBy(g => g.Title.Length)
            .ToList();

        result.Users = users.Select(u => new UserSearchDto
        {
            Id = u.Id,
            UserName = u.UserName,
            ProfileImageUrl = u.ProfileImageUrl,
            Country = u.Country
        }).ToList();

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "🔥 SearchService error: {Message}", ex.Message);
        Console.WriteLine($"🔥 [SearchService Error] {ex.Message}");
        return new SearchResultDto();
    }
}

}
