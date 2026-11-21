using System.Collections.Immutable;
using System.Diagnostics;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Scripts.Fights;

/// <summary>Replays every turn the characters played in the captures through the fight's AI, and sets what it foresaw against what happened.</summary>
/// <remarks>
/// <para>
/// At each turn's start, the AI plans from the fight as the bot would imagine it (<see cref="ImaginedFights"/>), and the turn
/// the player or the AI played is imagined too, from the casts and walks the server announced: both are judged by the same
/// numbers, the damage dealt, the kills and the heals foreseen, then the life the enemies' reply takes (D206).
/// </para>
/// <para>
/// At each turn's end, what the judge reckons is set against what came: the threat against the life the character lost
/// before its next turn, the enemies' reply as the search plays it against the same, and the approach against the damage
/// it dealt in its next turn. It prints counts and sums, never a value of one fight.
/// </para>
/// <para>
/// A turn of a fight whose challenge is easy (D466) is planned once more keeping it, as the bot would with a profile's
/// default, the fight's risk aside: the turns whose best plan keeps it already, those the search keeps it in within what its
/// bonus pays, and those it leaves, by challenge; and how long both searches last.
/// </para>
/// <para>
/// A summon's turn the client played is replayed the same, the AI's plan against the player's (D271); the summons planned
/// and played are counted by breed and spell, and what the summons deal in the reply (D270).
/// </para>
/// <para>
/// The main counts are printed again by the character's breed, a section each, with the reply's blows against the
/// enemies', alone and in a group (D463).
/// </para>
/// <para>
/// Each plan of nothing with MP left is counted by what the turn held as it began, as the live bench counts the idle turns
/// (D480); one far from the enemies with no spell landing, by whether a walk beat ending the turn now before the reply,
/// after it, or neither, and what the threat and next turn's damage did to its best walk nearer (D487).
/// </para>
/// <para>
/// Each cast played that took life from the character's team but itself, what the engine foresaw of it against what the
/// server announced, then the casts planned that it foresees doing so, by spell, on the whole team and on its characters
/// alone (D485).
/// </para>
/// </remarks>
internal sealed class TurnBench
{
    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly FightTactics _tactics;
    private readonly EffectActions _actions;
    private readonly TacticWeights _weights;
    private readonly TurnSearchLimits _limits;
    private readonly double _enemyLife;
    private readonly Dictionary<long, FightMap> _fightMaps = [];
    private readonly List<double> _searches = [];
    private readonly List<double> _challenged = [];
    private readonly HashSet<int> _left = [];
    private readonly Dictionary<string, double> _sums = new(StringComparer.Ordinal);
    private readonly BreedSections _byBreed = new();

    public Tally Tally { get; } = new();

    public TurnBench(GameTables tables, GameMaps maps, TacticWeights weights, TurnSearchLimits limits, double enemyLife = 1)
    {
        _tables = tables;
        _maps = maps;
        _tactics = new FightTactics(tables);
        _actions = new EffectActions(tables.Effects);
        _weights = weights;
        _limits = limits;
        _enemyLife = enemyLife;
    }

    private static double Effective(Combatant fighter)
    {
        return fighter.IsAlive ? fighter.Life + Math.Max(0, fighter.Characteristic(FightCharacteristic.Shield)) : 0;
    }

    private static bool InGroup(ImaginedFight fight)
    {
        // Beside another character of the team still alive, as the bot weighs a group's fight (D454): the reply measured apart.
        return fight.Fighters.Any(fighter => fighter.IsAlive && fighter.IsCharacter && fighter.Team == fight.Actor.Team && fighter.Id != fight.ActorId);
    }

    private static string Company(bool group)
    {
        return group ? "in a group" : "alone";
    }

    private static void Row(TextWriter writer, string label, double planned, double played)
    {
        writer.WriteLine($"  {label,-40}{planned,10:F0}{played,10:F0}");
    }

    private static (double Dealt, int Kills, double Healed) Outcome(ImaginedFight before, ImaginedFight after)
    {
        var team = before.Actor.Team;
        double dealt = 0, healed = 0;
        var kills = 0;

        foreach (var fighter in before.Fighters)
        {
            if (!fighter.IsAlive || after.Find(fighter.Id) is not { } then)
                continue;

            var change = Effective(then) - Effective(fighter);

            if (fighter.Team != team)
            {
                dealt += Math.Max(0, -change);
                kills += then.IsAlive ? 0 : 1;
            }
            else
            {
                healed += Math.Max(0, change);
            }
        }

        return (dealt, kills, healed);
    }

    public void Replay(List<IMessage> messages)
    {
        CharacterState? state = null;
        Turn? turn = null;
        Turn? summonTurn = null;
        Ending? ending = null;

        foreach (var message in messages)
        {
            if (message is CharacterSelectionEvent { Success.Character: { } chosen })
            {
                state = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);
                turn = null;
                ending = null;
            }

            if (state is null)
                continue;

            var self = state.Key.CharacterId;

            if (turn is not null && state.Fight is { } before)
                Observe(before, self, message, turn, state.Weapon is { } weapon && _tactics.Blows.TryGetBlow(weapon.ItemId, out var blow) ? blow : null);

            if (summonTurn is not null && state.Fight is { } summonBefore)
                Observe(summonBefore, summonTurn.Start.ActorId, message, summonTurn, null);

            // Whom the enemies' blows took life from between two of the character's turns, as the server announced it.
            if (ending is not null && message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.LifeLost } lost && state.Fight is { } between
                && between.Fighters.TryGetValue(self, out var own) && between.Fighters.TryGetValue(lost.LifeLost.TargetId, out var hit) && hit.Team == own.Team
                && between.Fighters.TryGetValue(lost.SourceId, out var source) && source.Team != own.Team)
            {
                var on = hit.Id == self ? "the character" : hit.SummonerId is not null ? "a summon" : "another ally";
                var life = lost.LifeLost.Loss + (lost.LifeLost.HasShieldLoss ? lost.LifeLost.ShieldLoss : 0);

                Add($"enemies' blows between turns: life, on {on}", life);
                _byBreed.Add(ending.Breed, $"reply {Company(ending.Group)}, dealt: on {on}", life);
            }

            state = CharacterEvents.Apply(state, RelayDirection.FromServer, message);

            switch (message)
            {
                // A summon the client controls plays its turn as the character does (9f2), the turns between left alone.
                case FightTurnStartPlayingEvent when state.Fight is { IsOwnTurn: true, PlayingId: { } summon } && summon != self && Imagined(state, null) is { } summoned:
                    summonTurn = new Turn(summoned.Fight, summoned.Knowledge, (summoned.Fight.Actor.ActionPoints, summoned.Fight.Actor.MovementPoints));
                    Plan(summonTurn, state.Fight);

                    break;

                case FightTurnEndEvent ended when summonTurn is not null && ended.FighterId == summonTurn.Start.ActorId:
                    FinishSummon(summonTurn);
                    summonTurn = null;

                    break;

                case FightTurnStartPlayingEvent when state.Fight is { IsOwnTurn: true } && Imagined(state, null) is { } start:
                    turn = new Turn(start.Fight, start.Knowledge, (start.Fight.Actor.ActionPoints, start.Fight.Actor.MovementPoints));

                    if (ending is not null)
                    {
                        Settle(ending, start.Fight);
                        turn.Foreseen = ending.NextTurn;
                        turn.Searched = ending.Searched;
                    }

                    ending = null;
                    Plan(turn, state.Fight);

                    break;

                case FightTurnEndEvent ended when ended.FighterId == self && turn is not null:
                    ending = Finish(turn, state);
                    turn = null;

                    break;

                case FightEndEvent:
                    _left.Clear();

                    if (ending is not null && state.Fight is { } over)
                        Settle(ending, null, over.Fighters.GetValueOrDefault(self));

                    turn = null;
                    summonTurn = null;
                    ending = null;

                    break;
            }
        }
    }

    public void Write(TextWriter writer)
    {
        Tally.Write(writer);
        writer.WriteLine("sums");

        foreach (var (key, sum) in _sums.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            writer.WriteLine($"  {sum,12:F0} {key}");

        if (_searches.Count > 0)
        {
            _searches.Sort();
            writer.WriteLine($"searches: {_searches.Count}, halfway {_searches[_searches.Count / 2]:F0} ms, 90% {_searches[(int)(_searches.Count * 0.9)]:F0} ms, at most {_searches[^1]:F0} ms");
        }

        if (_challenged.Count > 0)
        {
            _challenged.Sort();
            writer.WriteLine($"searches keeping a challenge: {_challenged.Count}, halfway {_challenged[_challenged.Count / 2]:F0} ms, 90% {_challenged[(int)(_challenged.Count * 0.9)]:F0} ms, at most {_challenged[^1]:F0} ms");
        }

        WriteBreeds(writer);
    }

    private void WriteBreeds(TextWriter writer)
    {
        // The reply against the enemies' blows for every breed first, then each breed's section (D463).
        string[] targets = ["the character", "another ally", "a summon"];

        writer.WriteLine("reply's blows against the enemies' blows between turns, every breed");
        writer.WriteLine($"  {string.Empty,-40}{"foreseen",10}{"dealt",10}{"ratio",8}");

        foreach (var company in new[] { "alone", "in a group" })
        {
            writer.WriteLine($"  {company}, {_byBreed.Total($"reply {company}: turns"):F0} turns");

            foreach (var on in targets)
            {
                var foreseen = _byBreed.Total($"reply {company}, foreseen: on {on}");
                var dealt = _byBreed.Total($"reply {company}, dealt: on {on}");

                writer.WriteLine($"    {"on " + on,-38}{foreseen,10:F0}{dealt,10:F0}{BreedSections.Ratio(foreseen, dealt),8}");
            }
        }

        foreach (var breed in _byBreed.Breeds)
        {
            writer.WriteLine($"by breed: {BreedSections.Name(breed)}, {_byBreed.Of(breed, "turns: replayed"):F0} turns replayed");
            writer.WriteLine($"  plans: casts {_byBreed.Of(breed, "plans: casts"):F0}, walks only {_byBreed.Of(breed, "plans: walks only"):F0}, nothing {_byBreed.Of(breed, "plans: nothing"):F0}");
            writer.WriteLine($"  planned against played, damage: more {_byBreed.Of(breed, "against the turn played: damage more"):F0}, as much {_byBreed.Of(breed, "against the turn played: damage as much"):F0}, less {_byBreed.Of(breed, "against the turn played: damage less"):F0}");
            writer.WriteLine($"  {string.Empty,-40}{"planned",10}{"played",10}");

            foreach (var key in new[] { "damage foreseen", "kills foreseen", "heals foreseen", "life the reply takes", "damage over two turns", "kills over two turns", "judged" })
                Row(writer, key, _byBreed.Of(breed, $"planned: {key}"), _byBreed.Of(breed, $"played: {key}"));

            writer.WriteLine($"  {"damage dealt, as the server announced",-40}{string.Empty,10}{_byBreed.Of(breed, "played: damage dealt, as the server announced"),10:F0}");
            writer.WriteLine($"  {"reply's blows against the enemies'",-40}{"foreseen",10}{"dealt",10}{"ratio",8}");

            foreach (var company in new[] { "alone", "in a group" })
            {
                var turns = _byBreed.Of(breed, $"reply {company}: turns");

                if (turns is 0)
                    continue;

                writer.WriteLine($"    {company}, {turns:F0} turns");

                foreach (var on in targets)
                {
                    var foreseen = _byBreed.Of(breed, $"reply {company}, foreseen: on {on}");
                    var dealt = _byBreed.Of(breed, $"reply {company}, dealt: on {on}");

                    if (foreseen >= 1 || dealt >= 1)
                        writer.WriteLine($"      {"on " + on,-36}{foreseen,10:F0}{dealt,10:F0}{BreedSections.Ratio(foreseen, dealt),8}");
                }
            }

            var hitting = _byBreed.Suffixes(breed, "on allies, played: casts, spell ").Union(_byBreed.Suffixes(breed, "on allies, planned: casts, spell ")).OrderBy(id => int.Parse(id, System.Globalization.CultureInfo.InvariantCulture)).ToList();

            if (hitting.Count > 0)
            {
                // Each sum twice: on the whole team but the character, then on its characters alone, no summon.
                writer.WriteLine($"  {"its casts on its team but itself",-40}{"played",8}{"foreseen",14}{"announced",14}{"planned",8}{"foreseen",14}");

                foreach (var spell in hitting)
                {
                    string Both(string key)
                    {
                        return $"{_byBreed.Of(breed, $"on allies, {key}, spell {spell}"),7:F0}/{_byBreed.Of(breed, $"on allies, {key.Replace(": foreseen", ": foreseen on characters", StringComparison.Ordinal).Replace(": announced", ": announced on characters", StringComparison.Ordinal)}, spell {spell}"),-6:F0}";
                    }

                    writer.WriteLine($"    {"spell " + spell,-38}{_byBreed.Of(breed, $"on allies, played: casts, spell {spell}"),8:F0} {Both("played: foreseen")} {Both("played: announced")}{_byBreed.Of(breed, $"on allies, planned: casts, spell {spell}"),8:F0} {Both("planned: foreseen")}");
                }
            }

            writer.WriteLine($"  {"spells, by id",-40}{"planned",10}{"played",10}");

            var spells = _byBreed.Suffixes(breed, "spells planned: ").Union(_byBreed.Suffixes(breed, "spells played: ")).OrderBy(id => int.Parse(id, System.Globalization.CultureInfo.InvariantCulture));

            foreach (var spell in spells)
                Row(writer, $"  spell {spell}", _byBreed.Of(breed, $"spells planned: {spell}"), _byBreed.Of(breed, $"spells played: {spell}"));
        }
    }

    private void Add(string key, double value)
    {
        _sums[key] = _sums.GetValueOrDefault(key) + value;
    }

    private (ImaginedFight Fight, FightKnowledge Knowledge)? Imagined(CharacterState state, (int, int)? turnPoints)
    {
        if (state.MapId is not { } mapId || !_maps.TryGet(mapId, out var data))
            return null;

        if (!_fightMaps.TryGetValue(mapId, out var map))
            _fightMaps[mapId] = map = FightMap.Of(data);

        var imagined = ImaginedFights.Of(state, map, _tables, _tactics.Blows, turnPoints);

        if (imagined is not { } found || _enemyLife is 1)
            return imagined;

        // The captured fights against enemies of more life, which no capture holds (D288): what the AI plans then.
        var team = found.Fight.Actor.Team;

        return (found.Fight with { Fighters = [.. found.Fight.Fighters.Select(Bigger)] }, found.Knowledge);

        Combatant Bigger(Combatant fighter)
        {
            return fighter.Team == team ? fighter : fighter with { Life = (int)(fighter.Life * _enemyLife), MaxLife = (int)(fighter.MaxLife * _enemyLife) };
        }
    }

    private void Plan(Turn turn, FightState? fight)
    {
        var clock = Stopwatch.StartNew();
        var plan = _tactics.Plan(turn.Start, turn.Knowledge, _weights, _limits);

        _searches.Add(clock.Elapsed.TotalMilliseconds);
        turn.Planned = plan;

        var casts = plan.Actions.Count(action => action.Kind is TacticActionKind.Cast);

        var kind = plan.IsEmpty ? "nothing" : casts is 0 ? "walks only" : "casts";

        Tally.Count($"{(turn.Start.Actor.IsSummon ? "summon plans" : "plans")}: {kind}");

        if (!turn.Start.Actor.IsSummon)
        {
            _byBreed.Add(turn.Start.Actor.BreedId, $"plans: {kind}");
            Tally.Count($"states at a turn's start: breed {turn.Start.Actor.BreedId,2}, [{string.Join(",", turn.Start.Actor.States.Order())}]");
            Tally.Count($"states at a turn's start: breed {turn.Start.Actor.BreedId,2}, round {Math.Min(turn.Start.Round, 3)}, [{string.Join(",", turn.Start.Actor.States.Order())}]");
        }

        if (plan.IsEmpty && !turn.Start.Actor.IsSummon && turn.Start.Actor.MovementPoints > 0)
            Passed(turn);

        // The fight's first easy challenge neither decided nor left, as the bot plays for it.
        var played = fight?.Challenges.FirstOrDefault(challenge => challenge.Succeeded is null && !_left.Contains(challenge.Id) && TurnChallenge.Of(_tables.Challenges.CompletionOf(challenge.Id), challenge.Bonus) is not null);

        if (played is null || TurnChallenge.Of(_tables.Challenges.CompletionOf(played.Id), played.Bonus) is not { } easy)
            return;

        clock.Restart();

        var kept = _tactics.Plan(turn.Start, turn.Knowledge, _weights, _limits, challenge: easy with { StartCell = turn.Start.Actor.Cell });

        _challenged.Add(clock.Elapsed.TotalMilliseconds);

        if (!kept.KeepsChallenge)
            _left.Add(played.Id);

        var how = !kept.KeepsChallenge ? "left" : kept.Actions.SequenceEqual(plan.Actions) ? "kept by the best plan" : "kept by the search";

        Tally.Count($"challenge {played.Id,4}: turns {how}");
        Tally.Count($"challenges: turns {how}");
    }

    private void Passed(Turn turn)
    {
        var fight = turn.Start;
        var actor = fight.Actor;
        var knowledge = turn.Knowledge;

        if (actor.Cell is Combatant.NoCell)
            return;

        var enemies = fight.Fighters.Where(fighter => fighter.IsAlive && fighter.Team != actor.Team && fighter.Cell is not Combatant.NoCell).ToList();
        var nearest = enemies.Count is 0 ? -1 : enemies.Min(enemy => MapGeometry.Distance(actor.Cell, enemy.Cell));
        var lands = knowledge.SpellsOf(actor.Id).Any(level => knowledge.Cooldowns.GetValueOrDefault(level.SpellId) is 0 && actor.ActionPoints >= CastRules.Modified(level, actor).ApCost
            && _tactics.Rules.Targets(fight.Map, fight.Fighters, actor, level, fight.History, fight.Round) is var cells && enemies.Any(enemy => cells.Contains(enemy.Cell)));
        var away = nearest switch
        {
            < 0 => "no enemy",
            <= 1 => "an enemy beside",
            <= 4 => "the nearest enemy 2 to 4 cells away",
            <= 8 => "the nearest enemy 5 to 8 cells away",
            _ => "the nearest enemy 9 cells away or more",
        };
        var breed = actor.BreedId is { } id ? BreedSections.Name(id) : "unknown breed";

        Tally.Count($"plans of nothing with MP: {away}, {(lands ? "a spell landing" : "no spell landing")}");

        if (nearest < 5 || lands)
            return;

        var judge = _tactics.JudgeOf(fight, knowledge, _weights);
        var later = knowledge with { Cooldowns = knowledge.Cooldowns.Where(pair => pair.Value > 1).ToImmutableDictionary(pair => pair.Key, pair => pair.Value - 1) };
        var here = judge.Judge(fight);
        var (hereReplied, hereStanding, hereGain) = Replied(fight);
        var bestBefore = double.NegativeInfinity;
        var bestAfter = double.NegativeInfinity;
        var nearerBefore = double.NegativeInfinity;
        (double Score, double Standing, double Gain, int Distance) atBestBefore = default;
        var nearerWalk = (Threat: 0.0, Next: 0.0);
        var hereThreat = judge.Threat(fight, actor.Id);
        var hereNext = judge.NextTurnDamage(fight);
        var reachable = _tactics.Moves.Reachable(fight.Map, fight.Fighters, actor);

        foreach (var (cell, left) in reachable)
        {
            if (cell == actor.Cell)
                continue;

            var walked = _tactics.Imagination.Walked(fight, cell, left);
            var before = judge.Judge(walked);
            var distance = enemies.Min(enemy => MapGeometry.Distance(cell, enemy.Cell));
            var (after, standing, gain) = Replied(walked);

            if (distance < nearest && before > nearerBefore)
            {
                nearerBefore = before;
                nearerWalk = (judge.Threat(walked, actor.Id) - hereThreat, judge.NextTurnDamage(walked) - hereNext);
            }

            if (before > bestBefore)
            {
                bestBefore = before;
                atBestBefore = (after, standing, gain, distance);
            }

            bestAfter = Math.Max(bestAfter, after);
        }

        var stage = reachable.Count <= 1 ? "no cell to walk to"
            : bestBefore <= here + 0.01 ? "no walk better than standing before the reply"
            : bestAfter <= hereReplied + 0.01 ? "walks better before the reply, none after"
            : "a walk better after the reply, the search missed it";

        Tally.Count($"far plans of nothing: {stage}");
        Tally.Count($"far plans of nothing: {stage}, {breed}");

        if (nearerBefore > double.NegativeInfinity)
        {
            var than = nearerBefore > here + 0.01 ? "better" : nearerBefore >= here - 0.01 ? "as good" : "worse";

            Tally.Count($"far plans of nothing: the best nearer walk before the reply {than} than standing");
            Tally.Count($"far plans of nothing: the best nearer walk {than}, its threat {(nearerWalk.Threat > 0.5 ? "more" : nearerWalk.Threat < -0.5 ? "less" : "the same")}, its next turn's damage {(nearerWalk.Next > 0.5 ? "more" : nearerWalk.Next < -0.5 ? "less" : "the same")}");
            Add($"far plans of nothing, the best nearer walk {than}: judged less standing's", nearerBefore - here);
            Add($"far plans of nothing, the best nearer walk {than}: threat less standing's", nearerWalk.Threat);
            Add($"far plans of nothing, the best nearer walk {than}: next turn's damage less standing's", nearerWalk.Next);
            Add($"far plans of nothing, the best nearer walk {than}: standing's next turn's damage", hereNext);
        }

        Add($"far plans of nothing, {stage}: standing, judged", here);
        Add($"far plans of nothing, {stage}: best walk, judged", bestBefore);
        Add($"far plans of nothing, {stage}: standing, replied", hereReplied);
        Add($"far plans of nothing, {stage}: best walk, replied", bestAfter);

        if (bestBefore > double.NegativeInfinity)
        {
            Add($"far plans of nothing, {stage}: best walk before, replied", atBestBefore.Score);
            Add($"far plans of nothing, {stage}: best walk before, standing after the reply less standing's", atBestBefore.Standing - hereStanding);
            Add($"far plans of nothing, {stage}: best walk before, next turn's gain less standing's", atBestBefore.Gain - hereGain);
            Tally.Count($"far plans of nothing, {stage}: best walk before, next turn {(atBestBefore.Gain > 0 ? "gains" : "gains nothing")}, standing's {(hereGain > 0 ? "gains" : "gains nothing")}");
            Tally.Count($"far plans of nothing, {stage}: best walk before, {(atBestBefore.Distance < nearest ? "nearer" : atBestBefore.Distance == nearest ? "as near" : "farther")}");
        }

        (double Score, double Standing, double Gain) Replied(ImaginedFight end)
        {
            var replied = _tactics.Replies(end, knowledge, judge);
            var gain = _tactics.NextTurn(replied, later, judge, _limits).Gain;

            return (judge.JudgeReplied(replied, gain), judge.Standing(replied), gain);
        }
    }

    private static void Observe(FightState fight, long self, IMessage message, Turn turn, SpellLevel? blow)
    {
        switch (message)
        {
            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast, Cast.Spell: { } spell } action when action.SourceId == self:
                // A cast the character chose is the first of its sequence of a spell (client.md); the others its effects cast.
                if (fight.Sequences is [.., { Type: SequenceType.Spell, HasCast: false } innermost] && innermost.AuthorId == self)
                    turn.Played.Add(TacticAction.Cast(spell.SpellId, spell.LevelId, action.Cast.CellId));

                break;

            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast, Cast.Spell: null } struck when struck.SourceId == self && blow is { } level:
                // A weapon's blow names no spell (D218): it is the worn weapon's, when D3B strikes that weapon's.
                if (fight.Sequences is [.., { Type: SequenceType.Weapon, HasCast: false } inner] && inner.AuthorId == self)
                    turn.Played.Add(TacticAction.Cast(WeaponBlows.SpellId, level.Id, struck.Cast.CellId));

                break;

            case SequenceStartEvent { Type: SequenceType.Weapon } weapon when weapon.AuthorId == self:
                turn.WeaponBlows++;

                break;

            case MapMovementEvent walked when walked.ActorId == self && walked.Cells.Count > 1:
                turn.Played.Add(TacticAction.Walk(walked.Cells[^1]));

                break;

            // The life the character's casts took from its team but itself, by the cast played it came of (the owner's report).
            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.LifeLost } lost when lost.SourceId == self && turn.Played.FindLastIndex(action => action.Kind is TacticActionKind.Cast) is >= 0 and var cast
                && lost.LifeLost.TargetId != self && fight.Fighters.TryGetValue(self, out var own) && fight.Fighters.TryGetValue(lost.LifeLost.TargetId, out var hit) && hit.Team == own.Team:
                turn.OnAllies[cast] = turn.OnAllies.GetValueOrDefault(cast) + lost.LifeLost.Loss + (lost.LifeLost.HasShieldLoss ? lost.LifeLost.ShieldLoss : 0);

                if (hit.SummonerId is null)
                    turn.OnCharacters[cast] = turn.OnCharacters.GetValueOrDefault(cast) + lost.LifeLost.Loss + (lost.LifeLost.HasShieldLoss ? lost.LifeLost.ShieldLoss : 0);

                break;
        }
    }

    private string Does(int levelId)
    {
        // What a cast mostly does, the first of these its level's effects do.
        if (!_tactics.Blows.Spells.TryGetLevel(levelId, out var level))
            return "level unknown";

        var kinds = _tactics.Blows.Spells.EffectsOf(level).ToArray().Select(effect => _actions.Of(effect.EffectId).Kind).ToHashSet();

        return kinds.Overlaps([EffectKind.Damage, EffectKind.Steal, EffectKind.FixedDamage]) ? "damage"
            : kinds.Contains(EffectKind.Detonate) ? "detonate"
            : kinds.Overlaps([EffectKind.Heal, EffectKind.FixedHeal, EffectKind.PercentHeal]) ? "heal"
            : kinds.Overlaps([EffectKind.Shield, EffectKind.LifeShield, EffectKind.LevelShield]) ? "shield"
            : kinds.Contains(EffectKind.Summon) ? "summon"
            : kinds.Overlaps([EffectKind.Push, EffectKind.Pull, EffectKind.Teleport, EffectKind.Swap]) ? "move"
            : kinds.Contains(EffectKind.Boost) ? "boost"
            : kinds.Contains(EffectKind.Deboost) ? "deboost"
            : kinds.Overlaps([EffectKind.StateSet, EffectKind.StateRemoved]) ? "state"
            : "other";
    }

    private static EffectElement BestElement(Combatant caster)
    {
        // The element its characteristics raise most: strength earth's, intelligence fire's, chance water's, agility air's.
        (EffectElement Element, int Characteristic)[] elements =
        [
            (EffectElement.Earth, FightCharacteristic.Strength),
            (EffectElement.Fire, FightCharacteristic.Intelligence),
            (EffectElement.Water, FightCharacteristic.Chance),
            (EffectElement.Air, FightCharacteristic.Agility),
        ];

        return elements.MaxBy(pair => caster.Characteristic(pair.Characteristic)).Element;
    }

    private EffectElement? ElementOf(in SpellLevel level, Combatant caster)
    {
        foreach (var effect in _tactics.Blows.Spells.EffectsOf(level))
        {
            var action = _actions.Of(effect.EffectId);

            if (action.Kind is EffectKind.Damage or EffectKind.Steal && (effect.Properties & SpellEffectProperties.ForClientOnly) is 0)
            {
                var element = EffectFormulas.Resolve(action.Element, caster);

                return element is EffectElement.Neutral ? EffectElement.Earth : element;
            }
        }

        return null;
    }

    private void Elements(Turn turn, IEnumerable<TacticAction> actions, string whose)
    {
        // Whether each blow is of the element the caster's characteristics raise most, and when not, whether the engine
        // foresaw a blow of that element, from where the caster stood on the same cell, dealing more.
        var fight = turn.Start;

        foreach (var action in actions)
        {
            var actor = fight.Actor;

            if (action.Kind is TacticActionKind.Cast && _tactics.Blows.Spells.TryGetLevel(action.LevelId, out var chosen) && ElementOf(chosen, actor) is { } element)
            {
                var best = BestElement(actor);
                var dealt = Dealt(fight, _tactics.Imagination.Cast(fight, chosen, action.Cell));
                var outcome = "in the element its characteristics raise most";

                if (element != best)
                {
                    var alternatives = turn.Knowledge.SpellsOf(actor.Id)
                        .Where(level => level.Id != chosen.Id && ElementOf(level, actor) == best && actor.ActionPoints >= CastRules.Modified(level, actor).ApCost
                            && _tactics.Rules.Check(fight.Map, fight.Fighters, actor, level, action.Cell, fight.History, fight.Round) is CastRefusal.None)
                        .Select(level => Dealt(fight, _tactics.Imagination.Cast(fight, level, action.Cell)) / Math.Max(1, (int)CastRules.Modified(level, actor).ApCost))
                        .ToList();
                    var perPoint = dealt / Math.Max(1, (int)CastRules.Modified(chosen, actor).ApCost);

                    outcome = alternatives.Count is 0 ? "in another, none of that element castable there"
                        : alternatives.Max() > perPoint + 0.5 ? "in another, one of that element foreseen dealing more a point"
                        : "in another, those of that element foreseen dealing no more a point";
                }

                Tally.Count($"blows {whose}: {outcome}");

                // By breed, the element each blow is of against the caster's best and what it was foreseen dealing, so that a
                // breed casting off its element shows apart, and whether its formulas or its judge chose so.
                var breed = actor.BreedId is { } id ? $"breed {id,2}" : "breed unknown";

                Tally.Count($"blows {whose} by breed: {breed}, {(element == best ? "best element" : "another element")}");
                Tally.Count($"blows {whose} by breed: {breed}, {(element == best ? "best element" : "another element")}, damage foreseen summed", (int)Math.Round(dealt));
                Tally.Count($"blows {whose} by breed and spell: {breed}, spell {chosen.SpellId} {element}, best {best}");

                if (element != best)
                    Tally.Count($"blows {whose} by breed, in another element: {breed}, {outcome}");
            }

            fight = Act(fight, [action]);
        }
    }

    private ImaginedFight Act(ImaginedFight fight, IEnumerable<TacticAction> actions)
    {
        foreach (var action in actions)
        {
            var actor = fight.Actor;

            if (!actor.IsAlive)
                break;

            if (action.Kind is TacticActionKind.Walk)
            {
                var reachable = _tactics.Moves.Reachable(fight.Map, fight.Fighters, actor);
                var left = reachable.TryGetValue(action.Cell, out var kept) ? kept : (Math.Max(0, actor.MovementPoints - MapGeometry.Distance(actor.Cell, action.Cell)), actor.ActionPoints);

                fight = _tactics.Imagination.Walked(fight, action.Cell, left);
            }
            else if (_tactics.Blows.Spells.TryGetLevel(action.LevelId, out var level))
            {
                fight = _tactics.Imagination.Cast(fight, level, action.Cell);
            }
        }

        return fight;
    }

    private static bool Idle(ImaginedFight before, ImaginedFight after)
    {
        if (after.Fighters.Length != before.Fighters.Length || after.Buffs.Length != before.Buffs.Length)
            return false;

        for (var index = 0; index < before.Fighters.Length; index++)
        {
            var was = before.Fighters[index];
            var now = after.Fighters[index];

            if ((now.Id == before.ActorId ? now with { ActionPoints = was.ActionPoints } : now) != was)
                return false;
        }

        return true;
    }

    private void States(ImaginedFight fight, IEnumerable<TacticAction> actions, string whose)
    {
        // The casts asking a state, those changing the character's states, as a Zobal's masks do, and those the engine
        // foresees doing nothing but spend AP, as a Zobal's shield cast on an enemy did (D226, D229). A change of the
        // character's states is told by its place among the turn's casts, and by the casts after it that ask a state it
        // set: a mask put on first to cast its spells, or last for the next turn (D250).
        var casts = actions.Count(action => action.Kind is TacticActionKind.Cast);
        var index = -1;
        var set = new HashSet<int>();

        foreach (var action in actions)
        {
            if (!fight.Actor.IsAlive)
                break;

            if (action.Kind is TacticActionKind.Walk || !_tactics.Blows.Spells.TryGetLevel(action.LevelId, out var level))
            {
                fight = Act(fight, [action]);

                continue;
            }

            var after = _tactics.Imagination.Cast(fight, level, action.Cell);

            var breed = fight.Actor.BreedId is { } id ? $"breed {id}" : "breed unknown";

            index++;

            if (level.StatesCriterion is not SpellLevel.NoCriterion)
            {
                Tally.Count($"states: casts {whose}, of a level asking a state, {breed}");
                Tally.Count($"states: casts {whose} asking a state, by spell, {breed}, spell {level.SpellId}");

                if (set.Count > 0 && _tactics.Rules.StatesAllow(level, after.Actor) && !_tactics.Rules.StatesAllow(level, fight.Actor with { States = [.. fight.Actor.States.Except(set)] }))
                    Tally.Count($"states: casts {whose}, of a level asking a state the turn's change set, {breed}");
            }

            if (!after.Actor.States.ToHashSet().SetEquals(fight.Actor.States))
            {
                Tally.Count($"states: casts {whose}, changing the character's states, {breed}");
                Tally.Count($"states: changes {whose} of the character's states, {(casts is 1 ? "the turn's only cast" : index is 0 ? "the turn's first cast" : index == casts - 1 ? "the turn's last cast" : "between casts")}, {breed}");
                set.UnionWith(after.Actor.States.Except(fight.Actor.States));
            }

            if (Idle(fight, after))
                Tally.Count($"states: casts {whose}, foreseen to do nothing but spend AP, {breed}");

            fight = after;
        }
    }

    private void FinishSummon(Turn turn)
    {
        // A controlled summon's turn, the AI's plan against the turn the player played, both imagined from its start.
        var played = Act(turn.Start, turn.Played);
        var planned = turn.Planned is { } plan ? Act(turn.Start, plan.Actions) : turn.Start;
        var monster = turn.Start.Actor.MonsterId is { } id ? $"monster {id}" : "monster unknown";

        Tally.Count($"summon turns: replayed, {monster}");
        Tally.Count("summon turns: casts played", turn.Played.Count(action => action.Kind is TacticActionKind.Cast));
        Tally.Count("summon turns: casts planned", turn.Planned?.Actions.Count(action => action.Kind is TacticActionKind.Cast) ?? 0);
        Add("summon turns: damage dealt, played", Dealt(turn.Start, played));
        Add("summon turns: damage dealt, planned", Dealt(turn.Start, planned));
    }

    private Ending Finish(Turn turn, CharacterState state)
    {
        var judge = _tactics.JudgeOf(turn.Start, turn.Knowledge, _weights);
        var played = Act(turn.Start, turn.Played);
        var planned = turn.Planned is { } plan ? Act(turn.Start, plan.Actions) : turn.Start;

        var breed = turn.Start.Actor.BreedId;

        Tally.Count("turns: replayed");
        _byBreed.Add(breed, "turns: replayed");

        foreach (var action in turn.Played.Where(action => action.Kind is TacticActionKind.Cast))
            _byBreed.Add(breed, $"spells played: {action.SpellId.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        foreach (var action in turn.Planned?.Actions.Where(action => action.Kind is TacticActionKind.Cast) ?? [])
            _byBreed.Add(breed, $"spells planned: {action.SpellId.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        foreach (var action in turn.Played.Where(action => action.Kind is TacticActionKind.Cast))
        {
            Tally.Count($"casts played, by what they do: {Does(action.LevelId)}");

            if (Does(action.LevelId) is "boost" or "state" && _tactics.Blows.Spells.TryGetLevel(action.LevelId, out var buffing))
            {
                foreach (var effect in _tactics.Blows.Spells.EffectsOf(buffing))
                {
                    var does = _actions.Of(effect.EffectId);

                    if (does.Kind is EffectKind.Boost or EffectKind.Deboost or EffectKind.StateSet)
                        Tally.Count($"buffs played: {does.Kind} {(does.Kind is EffectKind.StateSet ? $"state {effect.DiceNum}" : $"characteristic {does.Characteristic}")}, {effect.Duration} rounds, mask {_tactics.Blows.Spells.TargetMaskOf(effect)}, ap {buffing.ApCost}");
                }
            }
        }

        foreach (var action in turn.Planned?.Actions.Where(action => action.Kind is TacticActionKind.Cast) ?? [])
        {
            Tally.Count($"casts planned, by what they do: {Does(action.LevelId)}");

            if (Does(action.LevelId) is "summon")
                Tally.Count($"summons planned: breed {turn.Start.Actor.BreedId}, spell {action.SpellId}");
        }

        foreach (var action in turn.Played.Where(action => action.Kind is TacticActionKind.Cast && Does(action.LevelId) is "summon"))
            Tally.Count($"summons played: breed {turn.Start.Actor.BreedId}, spell {action.SpellId}");

        // A Rogue's turns (9f3): the bombs it summoned and set off, played and planned.
        if (turn.Start.Actor.BreedId is 13)
        {
            Tally.Count("bombs: a Roublard's turns");

            foreach (var (actions, what) in new (IReadOnlyList<TacticAction> Actions, string What)[] { (turn.Played, "played"), (turn.Planned?.Actions ?? [], "planned") })
            {
                var casts = actions.Where(action => action.Kind is TacticActionKind.Cast).ToList();

                Tally.Count($"bombs: summoned, {what}", casts.Count(action => _tactics.Blows.Spells.TryGetLevel(action.LevelId, out var level) && _tactics.Blows.Spells.EffectsOf(level).ToArray().Any(effect => effect.EffectId is 1008)));
                Tally.Count($"bombs: set off, {what}", casts.Count(action => Does(action.LevelId) is "detonate"));
            }

            // Its walls as the turn ends, and the enemies on them, whom they strike again as their turns begin (9f4).
            foreach (var (end, what) in new (ImaginedFight End, string What)[] { (played, "played"), (planned, "planned") })
            {
                var walls = new List<BombWallCell>();

                _tactics.Bombs.Walls(end.Map, end.Fighters, walls);
                Tally.Count($"bombs: wall cells as the turn ends, {what}", walls.Count);
                Tally.Count($"bombs: enemies on a wall as the turn ends, {what}", end.Fighters.Count(fighter => fighter.IsAlive && fighter.Team != end.Actor.Team && walls.Exists(wall => wall.Cell == fighter.Cell)));
            }
        }

        // The glyphs as the turn ends, and the enemies in those that strike as their turns begin (9f4).
        foreach (var (end, what) in new (ImaginedFight End, string What)[] { (played, "played"), (planned, "planned") })
        {
            var own = end.Marks.Where(mark => mark.AuthorId == turn.Start.ActorId && !turn.Start.Marks.Contains(mark)).ToList();

            if (own.Count is 0)
                continue;

            Tally.Count($"glyphs: set, breed {turn.Start.Actor.BreedId}, {what}", own.Count);
            Tally.Count($"glyphs: enemies in one striking as their turn begins, {what}", end.Fighters.Count(fighter => fighter.IsAlive && fighter.Team != end.Actor.Team && own.Exists(mark => mark.Kind is CellMarkKind.TurnStart && mark.Covers(fighter.Cell))));
        }

        States(turn.Start, turn.Played, "played");

        if (turn.Planned is { } planning)
            States(turn.Start, planning.Actions, "planned");

        // The weapon's blow, weighed as a spell's (D217): those the turns played, those the plans hold, and the turns whose
        // character could strike its sword's.
        if (turn.WeaponBlows > 0)
            Tally.Count("turns: with a weapon's blow", turn.WeaponBlows);

        if (turn.Knowledge.SpellsOf(turn.Start.ActorId).Any(level => WeaponBlows.IsBlow(level)))
        {
            Tally.Count("weapon's blow: turns of a character with a sword D3B strikes");
            Tally.Count("weapon's blow: blows played", turn.Played.Count(action => action.Kind is TacticActionKind.Cast && action.SpellId is WeaponBlows.SpellId));
            Tally.Count("weapon's blow: blows planned", turn.Planned?.Actions.Count(action => action.Kind is TacticActionKind.Cast && action.SpellId is WeaponBlows.SpellId) ?? 0);
        }

        Compare(turn, judge, played, planned);
        Allies(turn);
        Elements(turn, turn.Played, "played");

        if (turn.Planned is { } elementsPlanned)
            Elements(turn, elementsPlanned.Actions, "planned");

        // What the judge reckons from where the turn really ended, against what came before the character's next turn.
        var ended = Imagined(state, turn.Points) is { } real ? real.Fight : played;

        Add("played: damage dealt, as the server announced", Dealt(turn.Start, ended));
        _byBreed.Add(breed, "played: damage dealt, as the server announced", Dealt(turn.Start, ended));
        Add("played: kills, as the server announced", turn.Start.Fighters.Count(enemy => enemy.IsAlive && enemy.Team != turn.Start.Actor.Team && ended.Find(enemy.Id) is { IsAlive: false }));

        if (turn.Foreseen is { } foreseen)
        {
            Add("approach: foreseen as the turn before ended", foreseen);
            Add("approach: dealt", Dealt(turn.Start, ended));
            Tally.Count($"approach: {Band(foreseen, Dealt(turn.Start, ended))}");
        }

        if (turn.Searched is { } searched)
        {
            Add("next turn searched: foreseen as the turn before ended", searched);
            Tally.Count($"next turn searched: {Band(searched, Dealt(turn.Start, ended))}");
        }

        var endJudge = _tactics.JudgeOf(ended, turn.Knowledge, _weights);
        var self = ended.ActorId;
        var replied = _tactics.Replies(ended, turn.Knowledge, endJudge);
        var group = InGroup(turn.Start);

        _byBreed.Add(breed, $"reply {Company(group)}: turns");

        foreach (var ally in ended.Fighters.Where(fighter => fighter.IsAlive && fighter.Team == ended.Actor.Team))
        {
            if (replied.Find(ally.Id) is { } then)
            {
                var on = ally.Id == self ? "the character" : ally.IsSummon ? "a summon" : "another ally";
                var life = Math.Max(0, Effective(ally) - Effective(then));

                Add($"reply's blows: life, on {on}", life);
                _byBreed.Add(breed, $"reply {Company(group)}, foreseen: on {on}", life);
            }
        }

        var later = turn.Knowledge with { Cooldowns = turn.Knowledge.Cooldowns.Where(pair => pair.Value > 1).ToImmutableDictionary(pair => pair.Key, pair => pair.Value - 1) };
        var (next, _) = _tactics.NextTurn(replied, later, endJudge, _limits);

        return new Ending(
            Effective(ended.Actor),
            endJudge.Threat(ended, self),
            Math.Max(0, Effective(ended.Actor) - (replied.Find(self) is { } after ? Effective(after) : 0)),
            endJudge.NextTurnDamage(ended),
            Dealt(replied, next),
            breed,
            group);
    }

    private static int Kills(ImaginedFight start, ImaginedFight end)
    {
        return start.Fighters.Count(enemy => enemy.IsAlive && enemy.Team != start.Actor.Team && end.Find(enemy.Id) is { IsAlive: false });
    }

    private static (double Team, double Characters) OnTeam(ImaginedFight start, ImaginedFight end)
    {
        // The life a turn's casts take from the fighter's team but itself, as the engine imagines them, and of it from no summon.
        double taken = 0, characters = 0;

        foreach (var ally in start.Fighters)
        {
            if (ally.IsAlive && ally.Team == start.Actor.Team && ally.Id != start.ActorId && end.Find(ally.Id) is { } then)
            {
                taken += Math.Max(0, Effective(ally) - Effective(then));
                characters += ally.IsSummon ? 0 : Math.Max(0, Effective(ally) - Effective(then));
            }
        }

        return (taken, characters);
    }

    private void Allies(Turn turn)
    {
        // Each cast played, what the engine foresaw it take from the character's team against what the server announced,
        // then the casts the AI plans, foreseen: a cast that strikes an ally is told by its spell (the owner's report).
        var breed = turn.Start.Actor.BreedId;
        var fight = turn.Start;

        for (var index = 0; index < turn.Played.Count; index++)
        {
            var action = turn.Played[index];
            var after = Act(fight, [action]);

            if (action.Kind is TacticActionKind.Cast)
            {
                var (foreseen, characters) = OnTeam(fight, after);
                var announced = turn.OnAllies.GetValueOrDefault(index);
                var spell = action.SpellId.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (foreseen >= 1 || announced > 0)
                {
                    _byBreed.Add(breed, $"on allies, played: foreseen, spell {spell}", foreseen);
                    _byBreed.Add(breed, $"on allies, played: announced, spell {spell}", announced);
                    _byBreed.Add(breed, $"on allies, played: foreseen on characters, spell {spell}", characters);
                    _byBreed.Add(breed, $"on allies, played: announced on characters, spell {spell}", turn.OnCharacters.GetValueOrDefault(index));
                    _byBreed.Add(breed, $"on allies, played: casts, spell {spell}");
                    Tally.Count($"casts played on the character's team: {(foreseen < 1 ? "none foreseen, some announced" : announced is 0 ? "some foreseen, none announced" : "some foreseen, some announced")}");
                }
            }

            fight = after;
        }

        fight = turn.Start;

        foreach (var action in turn.Planned?.Actions ?? [])
        {
            var after = Act(fight, [action]);

            if (action.Kind is TacticActionKind.Cast && OnTeam(fight, after) is { Team: >= 1 } foreseen)
            {
                var spell = action.SpellId.ToString(System.Globalization.CultureInfo.InvariantCulture);

                _byBreed.Add(breed, $"on allies, planned: foreseen, spell {spell}", foreseen.Team);
                _byBreed.Add(breed, $"on allies, planned: foreseen on characters, spell {spell}", foreseen.Characters);
                _byBreed.Add(breed, $"on allies, planned: casts, spell {spell}");
            }

            fight = after;
        }
    }

    private static double Dealt(ImaginedFight start, ImaginedFight end)
    {
        var dealt = 0.0;

        foreach (var enemy in start.Fighters)
        {
            if (enemy.IsAlive && enemy.Team != start.Actor.Team && end.Find(enemy.Id) is { } then)
                dealt += Math.Max(0, Effective(enemy) - Effective(then));
        }

        return dealt;
    }

    private void Compare(Turn turn, FightJudge judge, ImaginedFight played, ImaginedFight planned)
    {
        var self = turn.Start.ActorId;
        var (playedDealt, playedKills, playedHealed) = Outcome(turn.Start, played);
        var (plannedDealt, plannedKills, plannedHealed) = Outcome(turn.Start, planned);
        var playedReply = _tactics.Replies(played, turn.Knowledge, judge);
        var plannedReply = _tactics.Replies(planned, turn.Knowledge, judge);
        var playedLost = Effective(played.Actor) - (playedReply.Find(self) is { } p ? Effective(p) : 0);
        var plannedLost = Effective(planned.Actor) - (plannedReply.Find(self) is { } q ? Effective(q) : 0);

        Add("played: damage foreseen", playedDealt);
        Add("played: kills foreseen", playedKills);
        Add("played: heals foreseen", playedHealed);
        Add("played: life the reply takes", Math.Max(0, playedLost));
        Add("planned: damage foreseen", plannedDealt);
        Add("planned: kills foreseen", plannedKills);
        Add("planned: heals foreseen", plannedHealed);
        Add("planned: life the reply takes", Math.Max(0, plannedLost));

        // What the character's summons deal in the reply, right after it and before the enemies (9f2).
        Add("played: damage its summons foresee in the reply", Dealt(played, playedReply));
        Add("planned: damage its summons foresee in the reply", Dealt(planned, plannedReply));

        // Over two turns: this turn's damage, then the next turn's as the search plays it from where the reply left the fight.
        var later = turn.Knowledge with { Cooldowns = turn.Knowledge.Cooldowns.Where(pair => pair.Value > 1).ToImmutableDictionary(pair => pair.Key, pair => pair.Value - 1) };
        var (playedNext, _) = _tactics.NextTurn(playedReply, later, judge, _limits);
        var (plannedNext, _) = _tactics.NextTurn(plannedReply, later, judge, _limits);

        Add("over two turns: played, damage foreseen", playedDealt + Dealt(playedReply, playedNext));
        Add("over two turns: planned, damage foreseen", plannedDealt + Dealt(plannedReply, plannedNext));
        Add("over two turns: played, kills foreseen", playedKills + Kills(playedReply, playedNext));
        Add("over two turns: planned, kills foreseen", plannedKills + Kills(plannedReply, plannedNext));
        Add("judged: played", judge.JudgeReplied(playedReply));
        Add("judged: planned", judge.JudgeReplied(plannedReply));

        var breed = turn.Start.Actor.BreedId;
        var playedTwo = playedDealt + Dealt(playedReply, playedNext);
        var plannedTwo = plannedDealt + Dealt(plannedReply, plannedNext);

        foreach (var (key, playedValue, plannedValue) in new (string, double, double)[]
        {
            ("damage foreseen", playedDealt, plannedDealt),
            ("kills foreseen", playedKills, plannedKills),
            ("heals foreseen", playedHealed, plannedHealed),
            ("life the reply takes", Math.Max(0, playedLost), Math.Max(0, plannedLost)),
            ("damage over two turns", playedTwo, plannedTwo),
            ("kills over two turns", playedKills + Kills(playedReply, playedNext), plannedKills + Kills(plannedReply, plannedNext)),
            ("judged", judge.JudgeReplied(playedReply), judge.JudgeReplied(plannedReply)),
        })
        {
            _byBreed.Add(breed, $"played: {key}", playedValue);
            _byBreed.Add(breed, $"planned: {key}", plannedValue);
        }

        var damage = plannedDealt > playedDealt + 0.5 ? "more" : plannedDealt < playedDealt - 0.5 ? "less" : "as much";

        _byBreed.Add(breed, $"against the turn played: damage {damage}");
        Tally.Count($"against the turn played: damage {damage}");
        Tally.Count($"against the turn played: kills {(plannedKills > playedKills ? "more" : plannedKills < playedKills ? "fewer" : "as many")}");
        Tally.Count($"against the turn played: life the reply takes {(plannedLost < playedLost - 0.5 ? "less" : plannedLost > playedLost + 0.5 ? "more" : "as much")}");
    }

    private void Settle(Ending ending, ImaginedFight? next, Fighter? over = null)
    {
        // The life the character lost from its turn's end to its next turn's start, or to the fight's end.
        var now = next is not null ? Effective(next.Actor) : over is { IsAlive: true } ? over.Life : 0;
        var lost = Math.Max(0, ending.Life - now);

        Tally.Count($"threat: {Band(ending.Threat, lost)}");
        Tally.Count($"reply: {Band(ending.Replied, lost)}");
        Add("threat: life lost", lost);
        Add("threat: foreseen", ending.Threat);
        Add("threat: off by", Math.Abs(ending.Threat - lost));
        Add("reply: foreseen", ending.Replied);
        Add("reply: off by", Math.Abs(ending.Replied - lost));

    }

    private static string Band(double foreseen, double lost)
    {
        if (foreseen < 1 && lost < 1)
            return "none foreseen, none lost";

        if (foreseen < 1)
            return "none foreseen, some lost";

        if (lost < 1)
            return "some foreseen, none lost";

        var ratio = foreseen / lost;

        return ratio < 0.5 ? "foreseen under half" : ratio > 2 ? "foreseen over twice" : "foreseen within half to twice";
    }

    private sealed class Turn
    {
        public Turn(ImaginedFight start, FightKnowledge knowledge, (int, int) points)
        {
            Start = start;
            Knowledge = knowledge;
            Points = points;
        }

        public ImaginedFight Start { get; }

        public FightKnowledge Knowledge { get; }

        public (int, int) Points { get; }

        public TurnPlan? Planned { get; set; }

        public List<TacticAction> Played { get; } = [];

        public int WeaponBlows { get; set; }

        public double? Foreseen { get; set; }

        public double? Searched { get; set; }

        public Dictionary<int, int> OnAllies { get; } = [];

        public Dictionary<int, int> OnCharacters { get; } = [];
    }

    private sealed record Ending(double Life, double Threat, double Replied, double NextTurn, double Searched, int? Breed, bool Group);
}
