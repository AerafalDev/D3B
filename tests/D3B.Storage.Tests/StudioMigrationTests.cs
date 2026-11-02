using D3B.Bot.Behaviors;
using D3B.Bot.Tests.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests;

public sealed class StudioMigrationTests
{
    private static async Task<long> CountAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";

        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A database as the version before the studio left it: a profile, a preset stored, a run, and a character playing the profile on connection.</summary>
    private static async Task WriteBeforeStudioAsync(TestStorage storage)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(storage.Settings.Path)!);

        var contexts = storage.Services.GetRequiredService<IDbContextFactory<StorageContext>>();

        await using (var context = await contexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
            await context.GetService<IMigrator>().MigrateAsync("Placements", TestContext.Current.CancellationToken);

        await using var connection = new SqliteConnection(storage.Settings.ConnectionString);

        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO "Accounts" ("Id", "FirstSeen", "LastSeen", "Nickname", "Tag") VALUES (7, '2026-10-30', '2026-10-30', 'nickname', '1234');
            INSERT INTO "Profiles" ("Id", "ActivityId", "Name", "Settings", "Updated") VALUES (1, 'harvest', 'Ash', '{}', '2026-10-30');
            INSERT INTO "Presets" ("ActivityId", "Key", "Stored") VALUES ('harvest', 'lumberjack', '2026-10-30');
            INSERT INTO "Characters" ("AccountId", "Id", "Name", "Level", "ProfileId", "StartsOnConnection") VALUES (7, 1, 'Character', 50, 1, 1);
            INSERT INTO "Runs" ("AccountId", "CharacterId", "CharacterName", "Activity", "Aggressions", "Banks", "Items", "Jobs", "LastAt", "Protectors", "StartedAt")
                VALUES (7, 1, 'Character', 'harvest', 0, 1, '{}', '[]', '2026-10-30', 0, '2026-10-30');
            """);
    }

    [Fact]
    public async Task TheStudioDropsTheProfilesPresetsAndRunsKeepsTheCharactersAndCopiesTheDatabaseFirst()
    {
        await using var storage = new TestStorage();

        await WriteBeforeStudioAsync(storage);
        await storage.StartedAsync();

        var copy = Path.Combine(Path.GetDirectoryName(storage.Settings.Path)!, "d3b.before-studio.db");
        var character = Assert.Single(storage.Accounts.Characters);

        Assert.Equal(("Character", (string?)null, false), (character.Name, character.Profile, character.StartsOnConnection));
        Assert.True(File.Exists(copy));
        Assert.Contains(storage.Log.GetSnapshot(), record => record.Message.StartsWith($"Copied the database {storage.Settings.Path} to {copy}", StringComparison.Ordinal));

        await using (var migrated = new SqliteConnection(storage.Settings.ConnectionString))
        {
            await migrated.OpenAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, await CountAsync(migrated, "Runs"));
            await Assert.ThrowsAsync<SqliteException>(() => CountAsync(migrated, "Profiles"));
        }

        // The copy holds what the migration dropped, whole.
        await using var kept = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = copy, Pooling = false }.ToString());

        await kept.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal((1L, 1L, 1L), (await CountAsync(kept, "Profiles"), await CountAsync(kept, "Presets"), await CountAsync(kept, "Runs")));
    }

    [Fact]
    public async Task ANewDatabaseIsNotCopied()
    {
        await using var storage = new TestStorage();

        await storage.StartedAsync();

        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(storage.Settings.Path)!, "d3b.before-studio.db")));
    }

    [Fact]
    public async Task TheVariablesACharacterKeepsAreReadBackByTheNextRun()
    {
        await using var storage = new TestStorage();

        await storage.StartedAsync();
        storage.Services.GetRequiredService<CharacterVariables>().Set(TestPlay.Key, "dungeons", 3);
        await storage.VariablesWrittenAsync();

        await using var next = new TestStorage(storage.Settings.Path);

        await next.StartedAsync();

        Assert.Equal(3, next.Services.GetRequiredService<CharacterVariables>().Get(TestPlay.Key, "dungeons")?.GetValue<int>());
    }
}
