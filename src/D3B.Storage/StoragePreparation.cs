using D3B.Bot.Hosting;
using D3B.Storage.Accounts;
using D3B.Storage.Graphics;
using D3B.Storage.Profiles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>Opens the database before the host starts: creates it, applies its migrations, and reads the accounts, the profiles and the graphics learned (D100, D138).</summary>
internal sealed class StoragePreparation : IHostPreparation
{
    private readonly StorageSettings _settings;
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly AccountBook _accounts;
    private readonly ProfileBook _profiles;
    private readonly GraphicRecorder _graphics;
    private readonly ILogger<StoragePreparation> _log;

    public StoragePreparation(StorageSettings settings, IDbContextFactory<StorageContext> contexts, AccountBook accounts, ProfileBook profiles, GraphicRecorder graphics, ILogger<StoragePreparation> log)
    {
        _settings = settings;
        _contexts = contexts;
        _accounts = accounts;
        _profiles = profiles;
        _graphics = graphics;
        _log = log;
    }

    public async Task<string?> PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settings.Path)!);

            var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            await using (context.ConfigureAwait(false))
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            await _accounts.LoadAsync(cancellationToken).ConfigureAwait(false);
            await _profiles.LoadAsync(cancellationToken).ConfigureAwait(false);

            var graphics = await _graphics.LoadAsync(cancellationToken).ConfigureAwait(false);

            StorageLog.Opened(_log, _settings.Path, _accounts.Accounts.Count, _accounts.Characters.Count, _profiles.Profiles.Count, graphics);

            return null;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or DbUpdateException)
        {
            return $"The database {_settings.Path} does not open: {exception.Message} Close what else holds it, or name another file with --{StorageSettings.PathKey} <file>.";
        }
    }
}
