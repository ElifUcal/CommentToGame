using System.Threading.Tasks;
using CommentToGame.DTOs;   // ← önemli

namespace CommentToGame.Services; // file-scoped ok

public interface IRawgClient
{
    Task<RawgPaged<RawgGameSummary>> GetGamesAsync(int page = 1, int pageSize = 40);
    Task<RawgGameDetail>? GetGameDetailAsync(int id);

    Task<RawgPaged<RawgGameSummary>> SearchGamesAsync(string query, int page = 1, int pageSize = 40);

    Task<RawgPaged<RawgGameSummary>> GetGameSeriesAsync(int id);

    Task<RawgPaged<RawgGameSummary>> GetGameAdditionsAsync(int id);

}