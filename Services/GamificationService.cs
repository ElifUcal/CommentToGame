using System;
using System.Threading.Tasks;
using CommentToGame.Config;
using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.enums;
using CommentToGame.Models;
using MongoDB.Driver;

public class GamificationService : IGamificationService
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<XpTransaction> _xpTransactions;

    public GamificationService(MongoDbService mongoDb)
    {
        var db = mongoDb.Database ?? throw new InvalidOperationException("Mongo database is null.");

        _users          = db.GetCollection<User>("User");
        _xpTransactions = db.GetCollection<XpTransaction>("xp_transactions");
    }

    public async Task AddXpAsync(string userId, XpSourceType sourceType, string? sourceId = null, string? uniqueKey = null)
    {
        // 1) XP değeri
        var xp = XpRules.GetXpForSource(sourceType);
        if (xp <= 0) return; // tanımlı değilse hiç uğraşma

        // 2) uniqueKey verilmişse daha önce bu event için XP verilmiş mi kontrol et
        if (!string.IsNullOrWhiteSpace(uniqueKey))
        {
            var filterUnique = Builders<XpTransaction>.Filter.Eq(x => x.UniqueKey, uniqueKey);
            var exists = await _xpTransactions.Find(filterUnique).AnyAsync();

            if (exists)
                return; // zaten XP verilmiş → tekrar verme
        }

        // 3) Kullanıcı var mı? (Id string olduğu için değer de string)
        var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var user = await _users.Find(userFilter).FirstOrDefaultAsync();
        if (user == null) return;

        // 4) XP transaction dokümanı oluştur
        var tran = new XpTransaction
        {
            UserId     = userId,
            SourceType = sourceType,
            SourceId   = sourceId,
            UniqueKey  = uniqueKey,
            Points     = xp,
            CreatedAt  = DateTime.UtcNow
        };

        await _xpTransactions.InsertOneAsync(tran);

        // 5) Kullanıcının XP / level hesaplaması
        var newTotalXp = (user.TotalXp) + xp;
        var newLevel   = CalculateLevelFromXp(newTotalXp);

        var updateDef = Builders<User>.Update
            .Set(u => u.TotalXp, newTotalXp)
            .Set(u => u.Level, newLevel);

        if (newLevel > user.Level)
        {
            updateDef = updateDef.Set(u => u.LastLevelUpAt, DateTime.UtcNow);
        }

        await _users.UpdateOneAsync(userFilter, updateDef);
    }

    private int CalculateLevelFromXp(int totalXp)
    {
        // Lineer: her 100 XP = 1 level
        if (totalXp < 0)
            totalXp = 0;

        var level = (totalXp / XpRules.LevelXpStep) + 1;
        return level;
    }

    public async Task<UserGamificationSummaryDto> GetUserSummaryAsync(string userId)
    {
        var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var user = await _users.Find(userFilter).FirstOrDefaultAsync();

        if (user == null)
        {
            return new UserGamificationSummaryDto
            {
                Level          = 1,
                TotalXp        = 0,
                CurrentLevelXp = 0,
                XpToNextLevel  = XpRules.LevelXpStep,
                Suggestions    = new()
            };
        }

        var level  = CalculateLevelFromXp(user.TotalXp);
        var xpStep = XpRules.LevelXpStep;

        var currentLevelStartXp = (level - 1) * xpStep;
        var currentLevelXp      = user.TotalXp - currentLevelStartXp;
        var xpToNext            = xpStep - currentLevelXp;

        return new UserGamificationSummaryDto
        {
            Level          = level,
            TotalXp        = user.TotalXp,
            CurrentLevelXp = currentLevelXp,
            XpToNextLevel  = xpToNext < 0 ? 0 : xpToNext,
            Suggestions    = new()
        };
    }
}
