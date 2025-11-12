// Data/SettingsRepository.cs
using CommentToGame.Data;
using MongoDB.Driver;

public class SettingsRepository
{
    private readonly IMongoCollection<AppSettings> _col;
    private readonly IMongoCollection<AppSettingsHistory> _hist;

    public SettingsRepository(MongoDbService db)
    {
        _col = db.GetCollection<AppSettings>("AppSettings");
        _hist = db.GetCollection<AppSettingsHistory>("AppSettingsHistory");
    }

    public async Task<AppSettings> GetAsync(CancellationToken ct)
    {
        var doc = await _col.Find(x => x.Id == "app_settings").FirstOrDefaultAsync(ct);
        if (doc == null)
        {
            var fresh = new AppSettings();
            await _col.InsertOneAsync(fresh, cancellationToken: ct);
            return fresh;
        }
        // Geriye dönük migration örneği:
        bool migrated = false;
        if (doc.Seo == null) { doc.Seo = new SeoSettings(); migrated = true; }
        // başka alanlar eklendiyse burada doldur
        if (migrated) await _col.ReplaceOneAsync(x => x.Id == doc.Id, doc, cancellationToken: ct);
        return doc;
    }

    public async Task<(bool ok, AppSettings saved, string? err)> UpdateAsync(AppSettings incoming, string user, CancellationToken ct)
    {
        var current = await GetAsync(ct);

        // Optimistic concurrency
        if (incoming.Version != current.Version)
            return (false, current, "VERSION_CONFLICT");

        // History kaydı
        await _hist.InsertOneAsync(new AppSettingsHistory
        {
            Version = current.Version,
            UpdatedAt = current.UpdatedAt,
            UpdatedBy = current.UpdatedBy,
            Snapshot = current
        }, cancellationToken: ct);

        // Merge kontrolü (gelen tüm alanları alıyoruz; istersen daha katı PATCH yaz)
        current.General = incoming.General;
        current.Security = incoming.Security;
        current.Content = incoming.Content;
        current.Seo = incoming.Seo;

        current.Version += 1;
        current.UpdatedAt = DateTime.UtcNow;
        current.UpdatedBy = user;

        await _col.ReplaceOneAsync(x => x.Id == current.Id, current, cancellationToken: ct);
        return (true, current, null);
    }
}
