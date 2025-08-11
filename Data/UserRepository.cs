using CommentToGame.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CommentToGame.Data;

public interface IUserRepository
{
    Task<bool> ExistsByUserNameOrEmail(string userName, string email);
    Task<User?> GetByUserName(string userName);
    Task<User?> GetByRefreshToken(string refreshToken);
    Task Create(User user);
    Task Update(User user);
    Task CreateIndexesIfNeeded();
}

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(IOptions<MongoDbSettings> options)
    {
        var cfg = options.Value;
        var client = new MongoClient(cfg.ConnectionString);
        var db = client.GetDatabase(cfg.DatabaseName);
        _users = db.GetCollection<User>(cfg.UsersCollection);
    }

    public Task CreateIndexesIfNeeded()
    {
        var keys = Builders<User>.IndexKeys.Ascending(u => u.UserName).Ascending(u => u.Email);
        var opts = new CreateIndexOptions { Unique = true, Name = "ux_username_email" };
        var model = new CreateIndexModel<User>(keys, opts);
        return _users.Indexes.CreateOneAsync(model);
    }

    public Task<bool> ExistsByUserNameOrEmail(string userName, string email) =>
        _users.Find(u => u.UserName == userName || u.Email == email).AnyAsync();

    public Task<User?> GetByUserName(string userName) =>
        _users.Find(u => u.UserName == userName).FirstOrDefaultAsync();

    public Task<User?> GetByRefreshToken(string refreshToken) =>
        _users.Find(u => u.RefreshToken == refreshToken).FirstOrDefaultAsync();

    public Task Create(User user) => _users.InsertOneAsync(user);

    public Task Update(User user) =>
        _users.ReplaceOneAsync(x => x.Id == user.Id, user);
}
