namespace CommentToGame.Data;

public class MongoDbSettings
{
    public required string ConnectionString { get; set; }
    public required string DatabaseName     { get; set; }
    public required string UsersCollection  { get; set; }
}
