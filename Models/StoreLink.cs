using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentToGame.Models;

public class StoreLink
{
    // JSON serializasyonu için Id zorumlu değil; embedded doküman olarak gidecek.
    public int? StoreId { get; set; }
    public string? Store { get; set; }      // "Steam", "PlayStation Store", ...
    public string? Slug { get; set; }       // "steam", "playstation-store"
    public string? Domain { get; set; }     // "store.steampowered.com"
    public string? Url { get; set; }        // tam mağaza adresi
    public string? ExternalId { get; set; } // örn: Steam AppID

    public double? Price { get; set; }
}