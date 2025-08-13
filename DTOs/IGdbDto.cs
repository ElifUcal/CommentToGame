using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommentToGame.DTOs
{
    public class IGdbDto
    {
         public sealed class IgdbPagedGames
    {
        public List<IgdbGameCard> Results { get; set; } = new();
        public string? Next { get; set; } // IGDB'yi v4 POST ile çağırırken siz dolduracaksınız (client tarafında)
    }

    public sealed class IgdbPagedSimpleNames
    {
        public List<IgdbSimpleName> Results { get; set; } = new();
    }

    public sealed class IgdbSimpleName
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class IgdbGameCard
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class IgdbGameDetail
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public int? Metacritic { get; set; } // IGDB: aggregated_rating/ rating yoksa null
        public double? Rating { get; set; }  // /100 gelir, biz /100 bırakıyoruz
        public string? BackgroundImage { get; set; } // cover/screenshot'tan üretilmiş URL
        public int? Added { get; set; } // yoksa null

        public List<string> Genres { get; set; } = new();
        public List<string> Platforms { get; set; } = new();
        public List<string> Developers { get; set; } = new();
        public List<string> Publishers { get; set; } = new();
        public List<string> AgeRatings { get; set; } = new(); // ESRB/PEGI adları
        public List<string> Tags { get; set; } = new();       // IGDB keywords -> isim listesi
    }
    }
}