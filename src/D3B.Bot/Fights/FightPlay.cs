using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Challenges;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>The AI playing the character's fight, one behavior from the placement to the end (D201, D202).</summary>
/// <remarks>
/// <para>
/// The bot sends every request the player would, the client following the server's answers as after its own (D60): the
/// placement's cell, its readiness, then each turn its walks and casts, one at a time, and the turn's end. Once the fight's
/// map is drawn, its display time, it judges the fight's odds, prudent when it would lose (D198), places the character on
/// the cell the AI judges best and says it is ready. Each turn it searches the best sequence (<see cref="FightTactics.Plan"/>),
/// sends its first action, waits for the server's answer and the client's showing it, and searches again from what the
/// server said; it ends its turn at once when nothing is worth doing, instead of waiting for the turn's time. A summon the
/// client controls plays its turns the same, with the same requests, which the server takes as the summon's (9f2).
/// </para>
/// <para>
/// With a profile's challenges (D466), each turn plays for the fight's challenge while it is one the AI keeps: of the
/// character's own turns, its blows or its kills (<see cref="TurnChallenge"/>, D497): the search keeps it while what it costs the turn stays within what
/// its bonus pays, and once a turn leaves it, it is left for the fight, a turn's challenge being lost then. A fight judged too
/// risky plays for none.
/// </para>
/// <para>
/// A cast the server refuses with an error text is not weighed again this turn, on that cell, and the next turn weighs it anew:
/// what it refuses depends on what it sees, such as a monster it cannot see. A cast it does not answer within its delay is not
/// cast again this fight, for a spell it never answers is one the character cannot cast; a walk not answered ends the turn.
/// The client acknowledges the sequences and says it is ready for each turn by itself (client.md).
/// </para>
/// </remarks>
public static class FightPlay
{
    private const int MostActionsATurn = 30;
    private const int MostRefusedATurn = 2;

    // The owner's ask (D203, D216): the AI's fight is closed to other players, then to spectators. A master's is left to its
    // party, which the server locks it to (D431, D435), and opened when the server kept it closed from the last (D471); a
    // follower's options are its master's.
    private static readonly (FightOption Option, bool On)[] _closing = [(FightOption.Closed, true), (FightOption.NoSpectators, true)];
    private static readonly (FightOption Option, bool On)[] _leading = [(FightOption.Closed, false), (FightOption.NoSpectators, true)];

    private static readonly TimeSpan _placementWait = TimeSpan.FromSeconds(45);

    // How long the AI waits for its placement's answer: the server's within 30 ms, measured (D450).
    private static readonly TimeSpan _placedWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _turnWait = TimeSpan.FromMinutes(5);

    /// <summary>Plays the character's fight to its end.</summary>
    /// <param name="context">The behavior's context, in a fight.</param>
    /// <param name="tactics">The fight's AI.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the fight ended for the character, once the server said; null when it ended unsaid.</returns>
    public static async Task<FightEnd?> PlayAsync(BotContext context, FightTactics tactics, GameTables tables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tactics);
        ArgumentNullException.ThrowIfNull(tables);

        var self = context.State.Key.CharacterId;
        var weights = TacticWeights.Default;
        var judged = false;
        var risky = false;
        var refused = new HashSet<int>();
        var challenged = new HashSet<int>();
        var left = new HashSet<int>();
        FightMap? map = null;

        // Not placed until this fight's placement places it, whatever the last fight left (D477).
        context.Actor.Team?.Placed(false);

        // The client draws the fight's map first, as it does a map out of fights (D77); a fight over meanwhile ends the wait.
        await context.WaitUntilAsync(Over, context.Delays.Display, cancellationToken).ConfigureAwait(false);

        try
        {
            while (true)
            {
                var state = context.State;

                if (state.Fight is not { Phase: not FightPhase.Ended } fight)
                    return Ended(context, state.Fight, challenged);

                foreach (var challenge in fight.Challenges.Where(challenge => challenged.Add(challenge.Id)))
                    FightLog.Challenged(context.Log, self, challenge.Id, challenge.Bonus, challenge.Targets.Length);

                if (state.MapId is not { } mapId || !context.Maps.TryGet(mapId, out var data))
                    return null;

                map = map?.Id == mapId ? map : FightMap.Of(data);

                if (!judged && ImaginedFights.Of(state, map, tables, tactics.Blows, null) is { } start)
                {
                    var odds = tactics.Odds(start.Fight, start.Knowledge);

                    weights = WeightsOf(state.Fight ?? fight, self, odds.IsRisky);
                    risky = odds.IsRisky;
                    judged = true;
                    FightLog.Judged(context.Log, self, odds.TurnsToWin, odds.TurnsToLose, odds.IsRisky);
                }

                if (fight.Phase is FightPhase.Placement)
                {
                    // Measured live: the fight's context comes before the server shows the character and its team's cells.
                    if (!Shown(fight, self))
                    {
                        await context.WaitUntilAsync(next => next.Fight is not { Phase: FightPhase.Placement } placing || Shown(placing, self), context.Delays.Placed, cancellationToken).ConfigureAwait(false);

                        continue;
                    }

                    // Placed once a fight when its readiness is the game's (D477), as it says nothing the server answers.
                    if (!fight.IsReady && context.Actor.Team?.IsPlaced != true)
                        await PlaceAsync(context, tactics, tables, map, weights, cancellationToken).ConfigureAwait(false);

                    await context.WaitUntilAsync(next => next.Fight is not { Phase: FightPhase.Placement }, _placementWait, cancellationToken).ConfigureAwait(false);

                    continue;
                }

                if (!fight.IsOwnTurn)
                {
                    await context.WaitUntilAsync(next => next.Fight is not { Phase: FightPhase.Fighting, IsOwnTurn: false }, _turnWait, cancellationToken).ConfigureAwait(false);

                    continue;
                }

                // The pack's challenges are read only once a turn may play for one.
                var play = PlayOf(context);
                var playedFor = play is ChallengePlay.None || risky || fight.Challenges.IsEmpty ? null : ChallengeOf(fight, tables.Challenges, play, risky, left, fight.PlayingId ?? self);

                await PlayTurnAsync(context, tactics, tables, map, weights, refused, playedFor, left, cancellationToken).ConfigureAwait(false);
                await context.WaitUntilAsync(next => next.Fight is not { IsOwnTurn: true }, context.Delays.Answer, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            context.Actor.Team?.FightOver();
        }
    }

    /// <summary>Gets the weights the AI plays a fight with: prudent when it would lose, a little bolder beside allies of its group (D454).</summary>
    /// <param name="fight">The fight, as the bot knows it.</param>
    /// <param name="self">The character.</param>
    /// <param name="risky">Whether the AI judged the fight too risky.</param>
    /// <returns>The weights.</returns>
    internal static TacticWeights WeightsOf(FightState fight, long self, bool risky)
    {
        if (risky)
            return TacticWeights.Prudent;

        var allies = fight.Fighters.Values.Any(fighter => fighter.Id != self && fighter.Team == fight.Team && fighter.IsAlive && fighter.BreedId is not null && fighter.SummonerId is null);

        return allies ? TacticWeights.Group : TacticWeights.Default;
    }

    /// <summary>Gets the challenge the AI plays for in a turn: the fight's first one it keeps not yet won, lost nor left (D466, D497).</summary>
    /// <param name="fight">The fight, as the bot knows it.</param>
    /// <param name="catalog">The client's challenges, whose completion criteria say what each asks.</param>
    /// <param name="play">Which challenges the profile plays for.</param>
    /// <param name="risky">Whether the AI judged the fight too risky: then none, the fight's safety first.</param>
    /// <param name="left">The challenges a turn left this fight.</param>
    /// <param name="fighterId">The fighter whose turn it is, the character or a summon it controls.</param>
    /// <returns>The challenge and what it asks of the turn; null when the AI plays for none.</returns>
    internal static (int Id, TurnChallenge Challenge)? ChallengeOf(FightState fight, ChallengeCatalog catalog, ChallengePlay play, bool risky, IReadOnlySet<int> left, long fighterId)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(left);

        if (play is ChallengePlay.None || risky)
            return null;

        foreach (var challenge in fight.Challenges)
        {
            if (challenge.Succeeded is null && !left.Contains(challenge.Id) && FightChallenges.TurnOf(fight, challenge, catalog, fighterId) is { } turn)
                return (challenge.Id, turn);
        }

        return null;
    }

    /// <summary>Gets the challenge a turn plays for as the server last told of it, before each plan of the turn (D497).</summary>
    /// <param name="played">The challenge and what it asked of the turn so far.</param>
    /// <param name="fight">The fight, as the bot knows it now.</param>
    /// <param name="catalog">The client's challenges.</param>
    /// <param name="fighterId">The fighter whose turn it is.</param>
    /// <returns>The challenge with what the turn's casts bound it to, its price and the turn's start kept; null once the server decided it.</returns>
    /// <remarks>The review's finding: a challenge the server said lost during the turn is no longer kept at any price.</remarks>
    internal static (int Id, TurnChallenge Challenge)? Refreshed((int Id, TurnChallenge Challenge) played, FightState? fight, ChallengeCatalog catalog, long fighterId)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (fight?.Challenges.FirstOrDefault(known => known.Id == played.Id) is not { } known)
            return played;

        if (known.Succeeded is not null)
            return null;

        return FightChallenges.TurnOf(fight, known, catalog, fighterId) is { } fresh
            ? (played.Id, fresh with { MostLoss = played.Challenge.MostLoss, StartCell = played.Challenge.StartCell })
            : played;
    }

    private static ChallengePlay PlayOf(BotContext context)
    {
        // As the bonus (D438): only a fight its activity has the AI play, so none with the host's Fights:Alone.
        return context.Actor.Activity is { IsOver: false, Activity: { FightsItsFights: true } activity } ? activity.Challenges : ChallengePlay.None;
    }

    private static FightEnd? Ended(BotContext context, FightState? fight, HashSet<int> challenged)
    {
        var self = context.State.Key.CharacterId;

        // Measured (D461): the server gives each challenge's result before the fight's end, so the ended fight holds them. A
        // challenge listed during a turn the fight ended in never reached the loop's top, where a challenge is said, and is said here, before its result.
        foreach (var challenge in fight?.Challenges ?? [])
        {
            if (challenged.Add(challenge.Id))
                FightLog.Challenged(context.Log, self, challenge.Id, challenge.Bonus, challenge.Targets.Length);

            FightLog.ChallengeEnded(context.Log, self, challenge.Id, challenge.Succeeded switch { true => "won", false => "lost", null => "with no result" });
        }

        return fight?.End;
    }

    private static bool Placing(FightState fight)
    {
        return fight is { Phase: FightPhase.Placement, IsReady: false };
    }

    private static bool Playing(FightState fight)
    {
        return fight is { Phase: FightPhase.Fighting, IsOwnTurn: true };
    }

    private static bool Shown(FightState fight, long self)
    {
        return fight.Fighters.TryGetValue(self, out var own) && own.Cell is not null && !fight.OwnCells.IsEmpty;
    }

    private static bool Over(CharacterState state)
    {
        return state.Fight is not { Phase: not FightPhase.Ended };
    }

    private static async Task<bool> PacedAsync(BotContext context, Func<FightState, bool> still, CancellationToken cancellationToken)
    {
        // The player's pace, so that the player sees each action (D202). Measured live (D203): a fight may end meanwhile, the
        // last enemy killed, and a request sent after its end left the bot waiting there, its player's next walk out of the
        // fight taken for the player's hand: the pace ends with the fight, and what it held is sent only while the fight asks it.
        await context.WaitUntilAsync(state => state.Fight is not { Phase: not FightPhase.Ended } fight || !still(fight), context.Delays.FightAction, cancellationToken).ConfigureAwait(false);

        return context.State.Fight is { Phase: not FightPhase.Ended } fight && still(fight);
    }

    private static async Task PlaceAsync(BotContext context, FightTactics tactics, GameTables tables, FightMap map, TacticWeights weights, CancellationToken cancellationToken)
    {
        var self = context.State.Key.CharacterId;

        if (ImaginedFights.Of(context.State, map, tables, tactics.Blows, null) is not { } imagined || context.State.Fight is not { } fight)
            return;

        var role = context.Actor.Team?.Role ?? FightRole.Alone;

        // Measured on the owner's game of 2026-11-20 (D471): a toggle, each; the server keeps a team's options from one fight of
        // its leader to the next, showing them as the placement starts, so that only those not yet as wanted are toggled.
        foreach (var (option, on) in role switch { FightRole.Leads => _leading, FightRole.Follows => [], _ => _closing })
        {
            // Read again after the pace: a toggle flips the option, and the server shows the options after the placement's cells,
            // measured, so that one not yet shown may come meanwhile.
            if (context.State.Fight?.Options.TryGetValue(option, out var shown) == true && shown == on)
                continue;

            if (!await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false) || context.State.Fight?.Options.GetValueOrDefault(option) == on)
                continue;

            var set = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is FightOptionUpdateEvent update && update.Option == option && update.Enabled == on);

            await context.SendAsync(new FightOptionToggleRequest { Option = option }, cancellationToken).ConfigureAwait(false);
            await set.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        }

        await ChooseBonusAsync(context, cancellationToken).ConfigureAwait(false);

        var cell = tactics.Place(imagined.Fight, fight.OwnCells, imagined.Knowledge, weights, (context.State.Fight ?? fight).TurnOrder);

        if (!await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false))
            return;

        if (cell != imagined.Fight.Actor.Cell)
        {
            var moved = context.Expect((side, _, state) => side is RelayDirection.FromServer && state.Fight?.Fighters.GetValueOrDefault(self)?.Cell == cell);

            await context.SendAsync(new FightPlacementPositionRequest { CellId = cell, FighterId = self }, cancellationToken).ConfigureAwait(false);
            // Measured on the owner's test of 2026-11-19 (D450): the server answers a move to a free cell within 30 ms; one to
            // a cell an ally chose at the same time it answers by swapping the two, which the state does not read, and
            // waiting its whole answer delay cost each such follower 3 s before it said it was ready.
            await moved.WaitAsync(_placedWait < context.Delays.Answer ? _placedWait : context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        }

        FightLog.Placed(context.Log, self, context.State.Fight?.Fighters.GetValueOrDefault(self)?.Cell ?? cell);

        // In a group the game follows, the server sets the follower ready as its master is (D477): the owner's ask, a readiness
        // of each follower is needless.
        if (context.Actor.Team is { LeavesReadyToTheGame: true } follower)
        {
            follower.Placed(true);

            return;
        }

        // A master is ready once its followers joining are placed (D434), lest the fight start without them.
        if (role is FightRole.Leads && context.Actor.Team is { } team)
            await team.PlacedAsync(context, _placementWait, cancellationToken).ConfigureAwait(false);

        if (!await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false))
            return;

        var ready = context.Expect((side, _, state) => side is RelayDirection.FromServer && state.Fight is { IsReady: true } or not { Phase: FightPhase.Placement });

        await context.SendAsync(new FightReadyRequest { Ready = true }, cancellationToken).ConfigureAwait(false);
        await ready.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ChooseBonusAsync(BotContext context, CancellationToken cancellationToken)
    {
        // The profile's choice, its step's in a list (D438), only for a fight its activity has the AI play: none out of one, nor
        // with the host's Fights:Alone over a harvest whose AI does not fight, whose hidden choice the player cannot see.
        ChallengeBonus? wanted = context.Actor.Activity is { IsOver: false, Activity: { FightsItsFights: true } activity } ? BonusOf(activity.ChallengeReward) : null;

        if (wanted is not { } bonus)
            return;

        // Measured (D438): the client sends its player's last choice by itself as the placement starts, the server answering
        // at once; sent before it, the bot's would be undone by it.
        await context.WaitUntilAsync(state => state.Fight is not { ChallengeBonus: null } || !Placing(state.Fight), context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        if (context.State.Fight?.ChallengeBonus == bonus || !await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false))
            return;

        var chosen = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is ChallengeBonusChoiceSelectedEvent selected && selected.Bonus == bonus);

        await context.SendAsync(new ChallengeBonusChoiceRequest { Bonus = bonus }, cancellationToken).ConfigureAwait(false);

        // Measured (D446): the server answers each member of a group, the bonus each character's own; unanswered, the
        // placement goes on.
        var answer = await chosen.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        FightLog.BonusChosen(context.Log, context.State.Key.CharacterId, bonus, answer is null ? "unanswered" : "set");
    }

    private static ChallengeBonus? BonusOf(ChallengeReward reward)
    {
        return reward switch
        {
            ChallengeReward.Drop => ChallengeBonus.Drop,
            ChallengeReward.Experience => ChallengeBonus.Experience,
            _ => null,
        };
    }

    private static async Task PlayTurnAsync(BotContext context, FightTactics tactics, GameTables tables, FightMap map, TacticWeights weights, HashSet<int> refusedSpells, (int Id, TurnChallenge Challenge)? challenge, HashSet<int> left, CancellationToken cancellationToken)
    {
        // The character's turn, or a summon's it controls.
        var self = context.State.Fight?.PlayingId ?? context.State.Key.CharacterId;
        var refused = new HashSet<(int SpellId, int Cell)>();
        var refusals = 0;
        var acted = 0;
        (int ActionPoints, int MovementPoints)? turnPoints = context.State.Fight?.Fighters.GetValueOrDefault(self) is { } own ? (own.ActionPoints, own.MovementPoints) : null;

        if (challenge is { } turn)
            challenge = (turn.Id, turn.Challenge with { StartCell = context.State.Fight?.Fighters.GetValueOrDefault(self)?.Cell });

        for (; acted < MostActionsATurn; acted++)
        {
            // What the server answered last is whole once its sequences closed.
            await context.WaitUntilAsync(state => state.Fight is not { Sequences.IsEmpty: false }, context.Delays.Answer, cancellationToken).ConfigureAwait(false);

            if (context.State.Fight is not { IsOwnTurn: true } || ImaginedFights.Of(context.State, map, tables, tactics.Blows, turnPoints) is not { } imagined)
                return;

            if (refusedSpells.Count > 0)
                imagined = (imagined.Fight, imagined.Knowledge.Without(refusedSpells));

            if (challenge is { } going)
                challenge = Refreshed(going, context.State.Fight, tables.Challenges, self);

            var plan = tactics.Plan(imagined.Fight, imagined.Knowledge, weights, TurnSearchLimits.Default, refused, challenge?.Challenge);

            FightLog.Planned(context.Log, self, plan.Actions.Length, plan.Score, plan.Standing, plan.Weighed, (long)plan.Elapsed.TotalMilliseconds);

            // A turn that breaks a challenge of the turns loses it: it is left for the fight. One the turn kept so far is kept
            // to its end, whatever is left of it to gain.
            if (challenge is { } played && !plan.KeepsChallenge)
            {
                left.Add(played.Id);
                FightLog.ChallengeLeft(context.Log, self, played.Id);
                challenge = null;
            }
            else if (challenge is { } kept)
            {
                challenge = (kept.Id, kept.Challenge.KeptThisTurn());
            }

            if (plan.IsEmpty)
                break;

            var action = plan.Actions[0];

            if (!await PacedAsync(context, Playing, cancellationToken).ConfigureAwait(false))
                return;

            if (action.Kind is TacticActionKind.Walk)
            {
                if (!await WalkAsync(context, tactics, imagined.Fight, action.Cell, cancellationToken).ConfigureAwait(false))
                    break;
            }
            else if (await CastAsync(context, self, action, cancellationToken).ConfigureAwait(false) is var answer and not CastAnswer.Cast)
            {
                // Measured live (D203): a spell the server leaves unanswered, such as a transformed character's own, is
                // refused again on every cell: it is not cast again this fight. One it refuses with a text depends on what
                // the server sees (D407): a target invisible for some turns left the spell out of the whole fight, and the
                // AI passed its turns once the monsters were seen again.
                refused.Add((action.SpellId, action.Cell));

                if (answer is CastAnswer.Unanswered)
                    refusedSpells.Add(action.SpellId);

                if (++refusals >= MostRefusedATurn)
                    break;
            }
        }

        // The owner's ask (D479): the turn's end waits for no pace once the turn acted. A turn with no action keeps it, and so
        // does one that killed the last enemy, whose fight the server may end meanwhile: a request sent after its end left
        // the bot waiting (D203).
        var paced = acted is 0 || context.State.Fight is { } after && !after.Fighters.Values.Any(fighter => fighter.IsAlive && fighter.Team != after.Team);

        if (context.State.Fight is not { Phase: not FightPhase.Ended, IsOwnTurn: true } || (paced && !await PacedAsync(context, Playing, cancellationToken).ConfigureAwait(false)))
            return;

        var ended = context.Expect((side, message, state) => side is RelayDirection.FromServer && (message is FightTurnEndEvent { FighterId: var fighter } && fighter == self || state.Fight is not { IsOwnTurn: true }));

        await context.SendAsync(new FightTurnFinishRequest(), cancellationToken).ConfigureAwait(false);
        await ended.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        FightLog.TurnEnded(context.Log, self, acted);
    }

    private static async Task<bool> WalkAsync(BotContext context, FightTactics tactics, ImaginedFight fight, int cell, CancellationToken cancellationToken)
    {
        var self = fight.ActorId;
        var path = tactics.PathTo(fight, cell);

        if (path.Length < 2 || context.State.MapId is not { } mapId)
            return false;

        var shown = Shown(context, self, SequenceType.Move);
        var walked = context.Expect((side, message, _) => side is RelayDirection.FromServer && (message is MapMovementEvent { ActorId: var actor } && actor == self || message is MapMovementRefusedEvent));

        await context.SendAsync(MapWalk.Request(mapId, new CellPath(path)), cancellationToken).ConfigureAwait(false);

        var answer = await walked.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        FightLog.Walked(context.Log, self, cell, path.Length - 1, answer?.Descriptor.Name ?? "unanswered");

        if (answer is not MapMovementEvent)
            return false;

        // The client shows a step as out of fights, then acknowledges the walk's sequence.
        if (await shown.WaitAsync(context.Delays.Confirmation(path.Length - 1), cancellationToken).ConfigureAwait(false) is null)
            FightLog.NotShown(context.Log, self, TacticActionKind.Walk);

        return true;
    }

    private static async Task<CastAnswer> CastAsync(BotContext context, long self, TacticAction action, CancellationToken cancellationToken)
    {
        // Measured (D218): the server frames a weapon's blow in a sequence of its own, which the client acknowledges as a spell's.
        var shown = Shown(context, self, action.SpellId is WeaponBlows.SpellId ? SequenceType.Weapon : SequenceType.Spell);
        // Measured live (D203): the server refuses a cast with an error text, 29 ms after the request, and nothing else.
        var cast = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && (message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast, SourceId: var source } && source == self
                || message is TextInformationEvent { Type: TextInformationType.Error }));

        await context.SendAsync(new GameActionFightCastRequest { SpellId = action.SpellId, CellId = action.Cell }, cancellationToken).ConfigureAwait(false);

        var answer = await cast.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        if (answer is TextInformationEvent refusal)
        {
            FightLog.Refused(context.Log, self, action.SpellId, action.Cell, refusal.TextId);

            return CastAnswer.Refused;
        }

        FightLog.Cast(context.Log, self, action.SpellId, action.Cell, answer is null ? "unanswered" : "cast");

        if (answer is null)
            return CastAnswer.Unanswered;

        if (await shown.WaitAsync(context.Delays.Answer + context.Delays.Display, cancellationToken).ConfigureAwait(false) is null)
            FightLog.NotShown(context.Log, self, TacticActionKind.Cast);

        return CastAnswer.Cast;
    }

    private static GameExpectation Shown(BotContext context, long self, SequenceType type)
    {
        // The client acknowledges a sequence of the character's once it showed it, naming the action its end gave.
        int? ended = null;

        return context.Expect((side, message, state) =>
        {
            if (side is RelayDirection.FromServer && message is SequenceEndEvent end && end.AuthorId == self && end.Type == type && ended is null)
                ended = end.ActionId;

            // A fight that ended needs nothing more shown.
            return (side is RelayDirection.FromClient && message is GameActionAcknowledgementRequest acknowledged && ended is { } id && acknowledged.ActionId == id)
                || state.Fight is not { Phase: not FightPhase.Ended };
        });
    }
}
