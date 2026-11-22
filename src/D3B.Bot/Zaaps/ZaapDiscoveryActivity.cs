using System.Collections.Frozen;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;

namespace D3B.Bot.Zaaps;

/// <summary>Walks to every zaap within the character's reach that it does not know, the nearest first, so that it learns each (D499).</summary>
/// <remarks>
/// <para>
/// Measured (client-game.md): the server lists the zaaps the character knows again, whole, as it enters the map of one it
/// did not know; using the zaap teaches nothing more. So each step travels to the nearest zaap left, by the world's path
/// with the zaaps known so far, and waits on its map for the server's list. The path is found again at each step, so a
/// zaap just learnt shortens the next journeys, and one learnt on the way is not sought any more.
/// </para>
/// <para>
/// Within reach is what the world's path may follow (D74, D83): a sub-area closed to an account without a subscription,
/// a transition whose criterion is false or that the bot cannot answer, such as a quest's, is not. A zaap no path leads
/// to, whose journey fails, or that the server does not list once the character stands on its map, is given up for the
/// run, the card telling a zaap the server did not teach from one out of reach; the activity is done once no zaap is
/// left, and fails in a haven bag it cannot leave.
/// </para>
/// </remarks>
public sealed class ZaapDiscoveryActivity : BotActivity
{
    // As the harvest's journeys by default (D167): a zaap costs kamas and saves little on a short way.
    private const int ZaapSaving = 5;

    // The list comes as the character enters the map, before what is on it, which the journey's arrival waited for.
    private static readonly TimeSpan _learning = TimeSpan.FromSeconds(5);

    private readonly HashSet<long> _unreachable = [];
    private readonly HashSet<long> _untaught = [];
    private readonly BusyWait _busy = new();
    private FrozenDictionary<long, int>? _sought;
    private volatile ActivityReport? _report;

    /// <inheritdoc/>
    public override ActivityReport? Report => _report;

    /// <inheritdoc/>
    public override int TeleportChanges => ZaapSaving;

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        // The recovery after an interruption puts the character back in order: the bounded wait counts anew (D484).
        _busy.Reset();

        return base.OnInterrupted(reason);
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _busy.WaitAsync(context, Failure, cancellationToken).ConfigureAwait(false))
            return true;

        var state = context.State;
        var character = state.Key.CharacterId;

        if (_sought is null)
        {
            _sought = ZaapTargets.Of(context.World.Atlas, state.KnownZaaps);
            ZaapLog.Seeking(context.Log, character, _sought.Count);
        }

        // Another's haven bag is never left (MapChange), and a leave the server refuses would be asked at every step.
        if (state.IsInHavenBag)
        {
            if (await context.LeaveHavenBagAsync(cancellationToken).ConfigureAwait(false) is not MapChangeOutcome.Changed)
                throw Failure("The character cannot leave the haven bag it is in.", "FailHavenBag");

            return true;
        }

        var left = Left(state);

        if (left.Count is 0)
        {
            var discovered = Discovered(state);

            Publish(state, GameText.Of(ZaapTexts.Text("Over")));
            ZaapLog.Over(context.Log, character, discovered, _sought.Count, _unreachable.Count, _untaught.Count);

            return false;
        }

        if (NearestOf(context, state, left) is not { } target)
        {
            foreach (var map in left)
                GiveUp(context, map, _unreachable, "no path leads there");

            return true;
        }

        Publish(state, GameText.Of(ZaapTexts.Text("Going"), GameTextValue.SubArea(_sought[target])));
        ZaapLog.Going(context.Log, character, target);

        switch (await context.TravelAsync(FrozenSet.Create(target), cancellationToken).ConfigureAwait(false))
        {
            case TravelOutcome.Arrived:
                Publish(context.State, GameText.Of(ZaapTexts.Text("Learning"), GameTextValue.SubArea(_sought[target])));

                if (await context.WaitUntilAsync(now => now.KnownZaaps.Contains(target), _learning, cancellationToken).ConfigureAwait(false))
                    ZaapLog.Learnt(context.Log, character, target);
                else
                    GiveUp(context, target, _untaught, "the server did not list it on its map");

                break;
            case TravelOutcome.Busy:
                await context.WaitUntilAsync(now => !now.IsBusy, BusyWait.Delay, cancellationToken).ConfigureAwait(false);
                break;
            case var outcome:
                GiveUp(context, target, _unreachable, $"the journey ended {outcome}");
                break;
        }

        return true;
    }

    private static long? NearestOf(BotContext context, CharacterState state, IReadOnlySet<long> left)
    {
        if (state.MapId is not { } mapId || state.Cell is not { } cell || !context.Maps.TryGet(mapId, out var map)
            || !context.World.TryGetVertex(map, cell, out var vertex)
            || !context.World.TryFind(vertex, left, WorldTravel.RulesOf(context), out var path))
        {
            return null;
        }

        return context.World.Graph.Vertices.Span[path.To].MapId;
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException(message, GameText.Of(ZaapTexts.Text(key)));
    }

    private FrozenSet<long> Left(CharacterState state)
    {
        return _sought!.Keys.Where(map => !state.KnownZaaps.Contains(map) && !_unreachable.Contains(map) && !_untaught.Contains(map)).ToFrozenSet();
    }

    private int Discovered(CharacterState state)
    {
        return _sought!.Keys.Count(state.KnownZaaps.Contains);
    }

    private void GiveUp(BotContext context, long map, HashSet<long> givenUp, string why)
    {
        givenUp.Add(map);
        ZaapLog.GivenUp(context.Log, context.State.Key.CharacterId, map, why);
        Publish(context.State, _report?.Doing ?? GameText.Of(ZaapTexts.Text("Over")));
    }

    private void Publish(CharacterState state, GameText doing)
    {
        var sought = _sought!;

        _report = new ActivityReport
        {
            Doing = doing,
            Progress = new ActivityProgress(GameText.Of(ZaapTexts.Text("Discovered")), Discovered(state), sought.Count),
            Warnings = [.. sought.Keys.Order().Where(map => _unreachable.Contains(map) || _untaught.Contains(map)).Select(map => GameText.Of(ZaapTexts.Text(_untaught.Contains(map) ? "Untaught" : "OutOfReach"), GameTextValue.SubArea(sought[map])))],
            Focus = new ActivityFocus { Zone = [.. sought.Keys.Order().Select(map => new ZoneMap(map, StateOf(state, map)))] },
        };
    }

    private ZoneMapState StateOf(CharacterState state, long map)
    {
        if (state.KnownZaaps.Contains(map))
            return ZoneMapState.Visited;

        return _unreachable.Contains(map) || _untaught.Contains(map) ? ZoneMapState.Unreachable : ZoneMapState.ToVisit;
    }
}
