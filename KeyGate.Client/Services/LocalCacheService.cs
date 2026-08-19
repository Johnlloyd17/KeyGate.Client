using KeyGate.Client.Models;
using SQLite;

namespace KeyGate.Client.Services;

[Table("LockScreenConfigCache")]
public class CachedLockScreenConfig
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ScheduledLogoutTime { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Source { get; set; } = "default";
}

public class LocalCacheService : IDisposable
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SaveLockScreenConfigAsync(LockScreenConfig config)
    {
        if (config is null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            var db = await GetConnectionAsync();
            var cached = new CachedLockScreenConfig
            {
                Id = 1,
                BackgroundImageUrl = config.BackgroundImageUrl,
                LogoUrl = config.LogoUrl,
                Title = config.Title,
                Subtitle = config.Subtitle,
                ScheduledLogoutTime = config.ScheduledLogoutTime,
                UpdatedAt = config.UpdatedAt,
                Source = config.Source
            };
            await db.InsertOrReplaceAsync(cached);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LockScreenConfig?> GetLockScreenConfigAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var db = await GetConnectionAsync();
            var cached = await db.Table<CachedLockScreenConfig>().Where(c => c.Id == 1).FirstOrDefaultAsync();
            if (cached is null)
            {
                return null;
            }

            return new LockScreenConfig
            {
                BackgroundImageUrl = cached.BackgroundImageUrl,
                LogoUrl = cached.LogoUrl,
                Title = cached.Title,
                Subtitle = cached.Subtitle,
                ScheduledLogoutTime = cached.ScheduledLogoutTime,
                UpdatedAt = cached.UpdatedAt,
                Source = cached.Source
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is null)
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "keygate.db3");
            _connection = new SQLiteAsyncConnection(
                path,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
            await _connection.CreateTableAsync<CachedLockScreenConfig>();
        }
        return _connection;
    }

    public void Dispose()
    {
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _connection = null;
        _gate.Dispose();
    }
}
