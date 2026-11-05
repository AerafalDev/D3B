using D3B.Bot.Hosting;
using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Npcs;
using D3B.Storage.Runs;
using D3B.Storage.Variables;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>Opens the database before the host starts: creates it, applies its migrations, and reads the accounts, the graphics learned, the resources' growing back, the fights' placement cells, the NPCs learned, the runs and the characters' variables (D100, D138, D145, D146, D275, D298, D383).</summary>
/// <remarks>
/// The migration that drops the profiles, presets and runs (D304) is preceded once by a copy of the database beside it,
/// <c>d3b.before-studio.db</c>, the owner's choice: nothing it drops is lost for good.
/// </remarks>
internal sealed class StoragePreparation : IHostPreparation
{
    /// <summary>The name of the migration that drops the profiles, presets and runs (D304).</summary>
    public const string StudioMigration = "Studio";

    /// <summary>The name of the copy made before it, beside the database.</summary>
    public const string StudioCopy = "d3b.before-studio.db";

    private readonly StorageSettings _settings;
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly AccountBook _accounts;
    private readonly GraphicRecorder _graphics;
    private readonly RegrowthRecorder _regrowths;
    private readonly PlacementRecorder _placements;
    private readonly NpcRecorder _npcs;
    private readonly RunBook _runs;
    private readonly VariableRecorder _variables;
    private readonly ILogger<StoragePreparation> _log;

    public StoragePreparation(StorageSettings settings, IDbContextFactory<StorageContext> contexts, AccountBook accounts, GraphicRecorder graphics, RegrowthRecorder regrowths, PlacementRecorder placements, NpcRecorder npcs, RunBook runs, VariableRecorder variables, ILogger<StoragePreparation> log)
    {
        _settings = settings;
        _contexts = contexts;
        _accounts = accounts;
        _graphics = graphics;
        _regrowths = regrowths;
        _placements = placements;
        _npcs = npcs;
        _runs = runs;
        _variables = variables;
        _log = log;
    }

    public async Task<string?> PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settings.Path)!);

            var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            await using (context.ConfigureAwait(false))
            {
                var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);

                if (File.Exists(_settings.Path) && pending.Any(migration => migration.EndsWith("_" + StudioMigration, StringComparison.Ordinal)))
                    await CopyAsync(cancellationToken).ConfigureAwait(false);

                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }

            await _accounts.LoadAsync(cancellationToken).ConfigureAwait(false);

            var graphics = await _graphics.LoadAsync(cancellationToken).ConfigureAwait(false);
            var regrowths = await _regrowths.LoadAsync(cancellationToken).ConfigureAwait(false);
            var placements = await _placements.LoadAsync(cancellationToken).ConfigureAwait(false);
            var (npcs, questions, outcomes) = await _npcs.LoadAsync(cancellationToken).ConfigureAwait(false);

            var variables = await _variables.LoadAsync(cancellationToken).ConfigureAwait(false);

            await _runs.LoadAsync(cancellationToken).ConfigureAwait(false);
            StorageLog.Opened(_log, _settings.Path, _accounts.Accounts.Count, _accounts.Characters.Count, graphics, regrowths, placements, npcs, questions, outcomes, _runs.Runs.Count, variables);

            return null;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or DbUpdateException)
        {
            return $"The database {_settings.Path} does not open: {exception.Message} Close what else holds it, or name another file with --{StorageSettings.PathKey} <file>.";
        }
    }

    private async Task CopyAsync(CancellationToken cancellationToken)
    {
        var copy = Path.Combine(Path.GetDirectoryName(_settings.Path)!, StudioCopy);

        // Once: a copy already there is the database before the first migration that dropped them.
        if (File.Exists(copy))
            return;

        // SQLite's own backup, so that the copy is whole, its journal included, whatever else reads the file.
        var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _settings.Path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());

        await using (source.ConfigureAwait(false))
        {
            var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = copy, Pooling = false }.ToString());

            await using (target.ConfigureAwait(false))
            {
                await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                await target.OpenAsync(cancellationToken).ConfigureAwait(false);
                source.BackupDatabase(target);
            }
        }

        StorageLog.CopiedBeforeStudio(_log, _settings.Path, copy);
    }
}
