using System.Threading.Tasks;
using CommentToGame.DTOs;
using CommentToGame.Models;   // ← önemli

namespace CommentToGame.Services; // file-scoped ok

public interface IRawgClient
{
    Task<RawgPaged<RawgGameSummary>> GetGamesAsync(int page = 1, int pageSize = 40);
    Task<RawgGameDetail?> GetGameDetailAsync(int id);

    Task<RawgPaged<RawgGameSummary>> SearchGamesAsync(string query, int page = 1, int pageSize = 40);

    Task<RawgPaged<RawgGameSummary>> GetGameSeriesAsync(int id);

    Task<RawgPaged<RawgGameSummary>> GetGameAdditionsAsync(int id);

    Task<RawgPaged<RawgGameStoreItem>> GetGameStoresAsync(int id);

    Task<RawgPagedCreators> GetGameDevelopmentTeamAsync(int id);

    // IRawgClient.cs
    Task<List<StoreLink>> GetStoreLinksAsync(int id, CancellationToken ct = default);



}