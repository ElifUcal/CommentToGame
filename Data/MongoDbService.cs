using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace CommentToGame.Data
{
    public class MongoDbService
    {
        public IMongoDatabase Database { get; }

        public MongoDbService(IConfiguration configuration)
        {
            var connStr     = configuration["MongoDb:ConnectionString"]
                              ?? throw new InvalidOperationException("MongoDb:ConnectionString missing.");
            var databaseName = configuration["MongoDb:DatabaseName"]
                              ?? throw new InvalidOperationException("MongoDb:DatabaseName missing.");

            var client = new MongoClient(connStr);
            Database   = client.GetDatabase(databaseName);
        }

        // İstersen yardımcı bir koleksiyon getter:
        public IMongoCollection<T> GetCollection<T>(string name) => Database.GetCollection<T>(name);
    }
}
