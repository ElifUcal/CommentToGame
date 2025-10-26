namespace CommentToGame.DTOs
{
    public sealed class StoreLinkDto
    {
        public int?    StoreId    { get; set; }
        public string? Store      { get; set; }
        public string? Slug       { get; set; }
        public string? Domain     { get; set; }
        public string? Url        { get; set; }
        public string? ExternalId { get; set; }
        public double? Price { get; set; }
    }
}