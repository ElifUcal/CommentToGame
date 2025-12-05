using System.Threading.Tasks;
using MongoDB.Driver;
using CommentToGame.Data;
using CommentToGame.Models;

namespace CommentToGame.Infrastructure;

public static class MongoIndexBootstrapper
{
    public static async Task CreateAsync(MongoDbService svc)
    {
        var db = svc.Database ?? throw new InvalidOperationException("Mongo database is null.");

        // --- Collections
        var games        = db.GetCollection<Game>("Games");
        var details      = db.GetCollection<Game_Details>("GameDetails");
        var galleries    = db.GetCollection<Gallery>("Galleries");
        var genres       = db.GetCollection<Genre>("Genres");
        var platforms    = db.GetCollection<Platform>("Platforms");
        var companies    = db.GetCollection<Company>("Companies");
        var timeToBeat   = db.GetCollection<Time_To_Beat>("TimeToBeat");
        var users        = db.GetCollection<User>("User");
        var reviews      = db.GetCollection<Reviews>("reviews");
        var reviewVotes    = db.GetCollection<ReviewVote>("review_votes");
        var reviewReplies  = db.GetCollection<ReviewReply>("review_replies");

        var xpTransactions = db.GetCollection<XpTransaction>("xp_transactions");

        // Case-insensitive unique için collation (EN, strength: Secondary)
        var ci = new Collation("en", strength: CollationStrength.Secondary);

        var tasks = new List<Task>
        {
            // --- USER INDEXES ---
            users.Indexes.CreateOneAsync(
                new CreateIndexModel<User>(
                    Builders<User>.IndexKeys.Ascending(u => u.Email),
                    new CreateIndexOptions { Name = "ux_users_email", Unique = true, Collation = ci }
                )
            ),
            users.Indexes.CreateOneAsync(
                new CreateIndexModel<User>(
                    Builders<User>.IndexKeys.Ascending(u => u.UserName),
                    new CreateIndexOptions { Name = "ux_users_username", Unique = true, Collation = ci }
                )
            ),
            users.Indexes.CreateOneAsync(
                new CreateIndexModel<User>(
                    Builders<User>.IndexKeys.Ascending(u => u.RefreshToken),
                    new CreateIndexOptions { Name = "ix_users_refreshtoken" }
                )
            ),

            // --- GAME INDEXES ---
            games.Indexes.CreateOneAsync(
                new CreateIndexModel<Game>(
                    Builders<Game>.IndexKeys.Ascending(x => x.Game_Name),
                    new CreateIndexOptions { Name = "ux_games_game_name", Unique = true, Collation = ci }
                )
            ),

            details.Indexes.CreateOneAsync(
                new CreateIndexModel<Game_Details>(
                    Builders<Game_Details>.IndexKeys.Ascending(x => x.GameId),
                    new CreateIndexOptions { Name = "ix_gamedetails_gameid" }
                )
            ),

            galleries.Indexes.CreateOneAsync(
                new CreateIndexModel<Gallery>(
                    Builders<Gallery>.IndexKeys.Ascending(x => x.GameId),
                    new CreateIndexOptions { Name = "ix_galleries_gameid" }
                )
            ),

            genres.Indexes.CreateOneAsync(
                new CreateIndexModel<Genre>(
                    Builders<Genre>.IndexKeys.Ascending(x => x.Name),
                    new CreateIndexOptions { Name = "ux_genres_name", Unique = true, Collation = ci }
                )
            ),

            platforms.Indexes.CreateOneAsync(
                new CreateIndexModel<Platform>(
                    Builders<Platform>.IndexKeys.Ascending(x => x.Name),
                    new CreateIndexOptions { Name = "ux_platforms_name", Unique = true, Collation = ci }
                )
            ),

            companies.Indexes.CreateOneAsync(
                new CreateIndexModel<Company>(
                    Builders<Company>.IndexKeys.Ascending(x => x.Company_Name),
                    new CreateIndexOptions { Name = "ux_companies_name", Unique = true, Collation = ci }
                )
            ),

            timeToBeat.Indexes.CreateOneAsync(
                new CreateIndexModel<Time_To_Beat>(
                    Builders<Time_To_Beat>.IndexKeys.Ascending(x => x.GameId),
                    new CreateIndexOptions { Name = "ux_timetoBeat_gameid", Unique = true }
                )
            ),

            // --- ✅ REVIEWS INDEX (YENİ EKLENDİ) ---
            reviews.Indexes.CreateOneAsync(
                new CreateIndexModel<Reviews>(
                    Builders<Reviews>.IndexKeys
                        .Ascending(x => x.GameId)
                        .Ascending(x => x.UserId),
                    new CreateIndexOptions
                    {
                        Name = "ux_reviews_game_user",
                        Unique = true
                    }
                )
            ),

            // --- REVIEWS PERFORMANCE INDEXES ---
    reviews.Indexes.CreateOneAsync(
        new CreateIndexModel<Reviews>(
            Builders<Reviews>.IndexKeys.Ascending(x => x.GameId),
            new CreateIndexOptions { Name = "ix_reviews_gameid" }
        )
    ),
    reviews.Indexes.CreateOneAsync(
        new CreateIndexModel<Reviews>(
            Builders<Reviews>.IndexKeys.Descending(x => x.TodayDate),
            new CreateIndexOptions { Name = "ix_reviews_createdat" }
        )
    ),
    reviews.Indexes.CreateOneAsync(
        new CreateIndexModel<Reviews>(
            Builders<Reviews>.IndexKeys
                .Ascending(x => x.GameId)
                .Descending(x => x.TodayDate),
            new CreateIndexOptions { Name = "ix_reviews_gameid_createdat" }
        )
    ),

    reviewVotes.Indexes.CreateOneAsync(
    new CreateIndexModel<ReviewVote>(
        Builders<ReviewVote>.IndexKeys
            .Ascending(x => x.ReviewId)
            .Ascending(x => x.UserId)
            .Ascending(x => x.Value),
        new CreateIndexOptions { Name = "ix_reviewvotes_review_user_value" }
    )
),

// --- REVIEW_REPLIES (lookup performansı) ---
reviewReplies.Indexes.CreateOneAsync(
    new CreateIndexModel<ReviewReply>(
        Builders<ReviewReply>.IndexKeys.Ascending(x => x.ReviewId),
        new CreateIndexOptions { Name = "ix_reviewreplies_reviewid" }
    )
),

        
        // --- ✅ XP TRANSACTIONS INDEXES ---
            xpTransactions.Indexes.CreateOneAsync(
                new CreateIndexModel<XpTransaction>(
                    Builders<XpTransaction>.IndexKeys.Ascending(x => x.UserId),
                    new CreateIndexOptions { Name = "ix_xp_userid" }
                )
            ),

            xpTransactions.Indexes.CreateOneAsync(
                new CreateIndexModel<XpTransaction>(
                    Builders<XpTransaction>.IndexKeys.Ascending(x => x.UniqueKey),
                    new CreateIndexOptions
                    {
                        Name   = "ux_xp_uniquekey",
                        Unique = true,
                        Sparse = true   // UniqueKey null olmayanlar için unique
                    }
                )
            ),
        };
        

        await Task.WhenAll(tasks);
    }
}
