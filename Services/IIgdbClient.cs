using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommentToGame.Models;
using static CommentToGame.DTOs.IGdbDto;

namespace CommentToGame.Services
{
    public interface IIgdbClient
    {
        // Sayfalı liste (RAWG:GetGamesAsync muadili)
        Task<IgdbPagedGames> GetGamesAsync(int page, int pageSize, CancellationToken ct = default);

        // Tekil oyun (RAWG:GetGameDetailAsync muadili)
        Task<IgdbGameDetail?> GetGameDetailAsync(long id, CancellationToken ct = default);

        // Arama (RAWG:SearchGamesAsync muadili)
        Task<IgdbPagedGames> SearchGamesAsync(string query, int page, int pageSize, CancellationToken ct = default);

        // DLC / Additions (RAWG:GetGameAdditionsAsync muadili)
        Task<IgdbPagedSimpleNames> GetGameAdditionsAsync(long id, CancellationToken ct = default);

        Task<IgdbTimeToBeat?> GetTimeToBeatAsync(long gameId, CancellationToken ct = default);

        Task<List<StoreLink>> GetStoreLinksAsync(long gameId, CancellationToken ct = default);

        Task<List<string>> GetAwardsLikeEventsAsync(long gameId, CancellationToken ct = default);

    }
}