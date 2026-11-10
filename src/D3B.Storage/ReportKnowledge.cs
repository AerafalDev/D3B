using System.Text.Json;
using System.Text.Json.Serialization;
using D3B.Bot.Reports;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage;

/// <summary>What the bots learned, as the file of a report: the fights' placement cells by map and the NPCs, their questions, replies and outcomes, by ids and no identity (D275, D383, D410).</summary>
/// <remarks>
/// Merging it into the project's data is a later tool and decision, what enters the pack and who validates (D410). It is checked in the dialog and removable.
/// </remarks>
public static class ReportKnowledge
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Reads the learned data from the database.</summary>
    /// <param name="contexts">The database.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The file <c>knowledge.json</c>.</returns>
    public static async Task<ReportPart> ReadAsync(IDbContextFactory<StorageContext> contexts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        await using var context = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Read into memory and sorted there: SQLite orders a DateTimeOffset as text, and the keys are all a reader needs.
        var knowledge = new
        {
            FightPlacements = (await context.Placements.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(row => row.FromMapId).ThenBy(row => row.MapId)
                .Select(row => new { row.FromMapId, row.FromAreaId, row.FromSubAreaId, row.PosX, row.PosY, row.MapId, row.RedCells, row.BlueCells, row.Seen, row.FirstSeenAt, row.LastSeenAt }),
            MapNpcs = (await context.MapNpcs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(row => row.MapId).ThenBy(row => row.NpcId)
                .Select(row => new { row.MapId, row.NpcId, row.ActorId, row.Cell, row.Direction, row.Seen, row.FirstSeenAt, row.LastSeenAt }),
            NpcQuestions = (await context.NpcQuestions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(row => row.MapId).ThenBy(row => row.NpcId).ThenBy(row => row.MessageId)
                .Select(row => new { row.MapId, row.NpcId, row.MessageId, row.Replies, row.Opened, row.Seen, row.FirstSeenAt, row.LastSeenAt }),
            NpcOutcomes = (await context.NpcOutcomes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(row => row.MapId).ThenBy(row => row.NpcId).ThenBy(row => row.MessageId).ThenBy(row => row.ReplyId)
                .Select(row => new { row.MapId, row.NpcId, row.MessageId, row.ReplyId, row.Kind, row.Target, row.Count, row.LastSeenAt }),
        };

        return new ReportPart(ReportFiles.Knowledge, JsonSerializer.SerializeToUtf8Bytes(knowledge, _json));
    }
}
