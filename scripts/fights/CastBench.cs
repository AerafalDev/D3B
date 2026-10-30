using System.Collections.Immutable;
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
using FightBuff = D3B.Protocol.Game.FightBuff;

namespace D3B.Scripts.Fights;

/// <summary>Replays fights through the bot's fight state, and compares each cast a fighter chose with what the server announced.</summary>
/// <remarks>
/// A cast a fighter chose is the first of its sequence of a spell (client.md): what follows until that sequence closes is
/// the cast's, the casts its effects trigger included, which the counts tell apart. D3B foresees the cast from the fight as
/// it stood before it, through <see cref="SpellEffects"/>; the server's actions are grouped by target and kind and set
/// against it.
/// </remarks>
internal sealed class CastBench
{
    private const int CriticalHits = 18;

    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly WeaponBlows _blows;
    private readonly CastRules _rules;
    private readonly SpellEffects _effects;
    private readonly EffectActions _actions;
    private readonly Imagination _imagination;
    private readonly TextWriter? _details;
    private readonly Dictionary<long, FightMap> _fightMaps = [];
    private readonly Dictionary<(long Fighter, int Spell), int> _turnsEnded = [];

    public Tally Tally { get; } = new();

    public CastBench(GameTables tables, GameMaps maps, TextWriter? details)
    {
        _tables = tables;
        _maps = maps;
        _blows = new WeaponBlows(tables.Spells, tables.Weapons, tables.Effects);
        _rules = new CastRules(_blows.Spells, tables.States);
        _actions = new EffectActions(tables.Effects);
        _effects = new SpellEffects(_blows, _rules, _actions);
        _imagination = new Imagination(_blows.Spells, tables.Monsters, _actions, _effects);
        _details = details;
    }


    private static string Outcome(LifeRange foreseen, int dealt, bool capped)
    {
        if (foreseen.Contains(dealt))
            return "within";

        if (capped && dealt <= foreseen.Max)
            return "within, capped by the target's life";

        return dealt < foreseen.Min ? "below" : "above";
    }

    private static int Steps(int from, int to)
    {
        // A slide moves along one of the map's eight directions: a step across, between two cells' corners, is one cell
        // of distance 1, a step along a row or column of the screen two (MapGeometry, D186).
        var a = MapGeometry.PointOf(from);
        var b = MapGeometry.PointOf(to);

        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    public void Replay(List<IMessage> messages)
    {
        var characters = CapturedFights.Characters(messages);
        var open = new List<Cast>();
        var levels = new Dictionary<(long Fighter, int Spell), SpellLevel>();
        FightState? fight = null;
        CharacterState? character = null;
        long self = 0, mapId = 0;

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];

            // The character's state, for the weapon it wears (D218).
            if (message is CharacterSelectionEvent { Success.Character: { } chosen })
                character = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);
            else if (character is not null)
                character = CharacterEvents.Apply(character, RelayDirection.FromServer, message);

            if (message is MapCurrentEvent current)
                mapId = current.MapId;

            if (message is FightStartingEvent)
            {
                fight = new FightState();
                self = characters.GetValueOrDefault(index);
                open.Clear();
                levels.Clear();
                Tally.Count("fights: replayed");
            }

            if (fight is null)
                continue;

            Before(fight, message, mapId, open, levels, character);
            FightEvents.TryApply(fight, self, message, out fight);

            if (message is SequenceEndEvent)
            {
                foreach (var ended in open.Where(cast => fight.Sequences.Length < cast.Depth).ToList())
                {
                    Settle(ended, fight);
                    open.Remove(ended);
                }
            }

            if (message is FightEndEvent)
                fight = null;
        }
    }

    private void Before(FightState fight, IMessage message, long mapId, List<Cast> open, Dictionary<(long, int), SpellLevel> levels, CharacterState? character)
    {
        // Where the state holds each fighter, set against where the server says it moves from: a slide's first cell and a
        // walk's, after every move the state follows.
        if (message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Slide } slid)
            Tally.Count($"positions: a slide from {(fight.Fighters.TryGetValue(slid.Slide.TargetId, out var slider) && slider.Cell == slid.Slide.FromCell ? "the cell the state holds" : "another cell")}");

        if (message is MapMovementEvent walked && walked.Cells.Count > 0 && fight.Phase is FightPhase.Fighting)
            Tally.Count($"positions: a walk from {(fight.Fighters.TryGetValue(walked.ActorId, out var walker) && walker.Cell == walked.Cells[0] ? "the cell the state holds" : "another cell")}");

        switch (message)
        {
            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast } action when action.Cast.Spell is not null
                || fight.Sequences is [.., { Type: SequenceType.Weapon, HasCast: false }]:
            {
                var struck = action.Cast.Spell is null;
                var chosen = fight.Sequences is [.., { Type: SequenceType.Spell or SequenceType.Weapon, HasCast: false } innermost] && innermost.AuthorId == action.SourceId;

                if (!chosen)
                {
                    foreach (var cast in open)
                        cast.TriggeredCasts++;

                    break;
                }

                SpellLevel level;

                if (struck)
                {
                    // A weapon's blow names no spell nor level (D218): the worn weapon's blow, when D3B strikes that weapon's.
                    var weapon = action.SourceId == character?.Key.CharacterId ? character.Weapon : null;
                    var kind = weapon is null ? "no weapon worn" : _tables.Weapons.TryGetWeapon(weapon.ItemId, out var held) ? $"a weapon of type {held.TypeId}" : "an item that is no weapon";

                    Tally.Count($"weapon's blows: struck with {kind}");

                    if (weapon is null || !_blows.TryGetBlow(weapon.ItemId, out level))
                        break;
                }
                else if (!_blows.Spells.TryGetLevel(action.Cast.Spell!.LevelId, out level))
                {
                    Tally.Count("casts: level or caster unknown");

                    break;
                }

                if (!fight.Fighters.TryGetValue(action.SourceId, out var caster))
                {
                    Tally.Count("casts: level or caster unknown");

                    break;
                }

                levels[(action.SourceId, level.SpellId)] = level;
                _turnsEnded[(action.SourceId, level.SpellId)] = 0;
                var map = MapOf(mapId);
                var fighters = FightCombatants.Of(fight, _tables.Monsters);
                var combatant = fighters.First(fighter => fighter.Id == action.SourceId);
                var refusal = _rules.Check(map, fighters, combatant, level, action.Cast.CellId, caster.Casts, fight.Round);

                Tally.Count($"rules of the casts chosen: {(refusal is CastRefusal.None ? "accepted" : $"refused, {refusal}")}");

                if (refusal is not CastRefusal.None)
                    _details?.WriteLine($"refused {refusal}: spell {level.SpellId} level {level.Id}, states {string.Join(",", combatant.States)}");

                if (struck)
                {
                    Tally.Count($"weapon's blows: the rules {(refusal is CastRefusal.None ? "accept" : $"refuse, {refusal}")}");
                    Tally.Count($"weapon's blows: {(action.Cast.Critical is FightCastCritical.Critical ? "critical" : "normal")}");
                }

                Tally.Count($"casts chosen: {(action.Cast.Critical is FightCastCritical.Critical ? "critical" : "normal")}");

                // The chance of a critical hit (D207): the level's, plus the caster's characteristic 18, by bands of ten.
                if (level.CriticalHitProbability > 0)
                {
                    var chance = Math.Clamp(level.CriticalHitProbability + combatant.Characteristic(CriticalHits), 0, 100);
                    var band = Math.Min(chance / 10 * 10, 90);

                    Tally.Count($"critical hits by the chance foreseen: {band,2}-{band + 9}%, casts");
                    Tally.Count($"critical hits by the chance foreseen: {band,2}-{band + 9}%, critical", action.Cast.Critical is FightCastCritical.Critical ? 1 : 0);
                    Tally.Count($"critical hits by the chance foreseen: {band,2}-{band + 9}%, chance foreseen summed", chance);
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, casts");
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, critical", action.Cast.Critical is FightCastCritical.Critical ? 1 : 0);
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, the level's chance summed", level.CriticalHitProbability);
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, the characteristic summed", combatant.Characteristic(CriticalHits));
                }
                open.Add(new Cast
                {
                    Caster = combatant,
                    Fighters = fighters,
                    Map = map,
                    Level = level,
                    Cell = action.Cast.CellId,
                    Critical = action.Cast.Critical is FightCastCritical.Critical,
                    Depth = fight.Sequences.Length,
                    Round = fight.Round,
                    Turn = fight.PlayingTurn,
                    Lives = fight.Fighters.Values.ToDictionary(fighter => fighter.Id, fighter => (fighter.Life, fighter.MaxLife)),
                    Held = fight.Fighters.Values.ToDictionary(fighter => fighter.Id, fighter => fighter.Buffs),
                });

                break;
            }

            case GameActionFightEvent action when open.Count > 0:
            {
                foreach (var cast in open)
                    cast.Actions.Add(action);

                break;
            }

            case FightEffectsEvent { Given.Buff: { } buff } given when open.Count > 0:
            {
                foreach (var cast in open)
                    cast.Buffs.Add((given.Given.ActionId, buff));

                break;
            }

            case FightTurnEndEvent ended:
            {
                foreach (var key in _turnsEnded.Keys.Where(key => key.Fighter == ended.FighterId).ToList())
                    _turnsEnded[key]++;

                break;
            }

            case RefreshCooldownEvent cooldowns when fight.Fighters.TryGetValue(cooldowns.FighterId, out var fighter):
            {
                foreach (var cooldown in cooldowns.Cooldowns)
                {
                    if (!levels.TryGetValue((fighter.Id, cooldown.SpellId), out var level))
                    {
                        Tally.Count($"cooldowns: of a spell not cast yet, {(cooldown.Rounds is 0 ? "0" : "more")}");

                        continue;
                    }

                    // Measured (D193): the server counts a spell's interval down as each of its caster's turns ends.
                    var ended = _turnsEnded.GetValueOrDefault((fighter.Id, cooldown.SpellId));
                    var expected = level.MinCastInterval > 0 && fighter.Casts.LastRounds.ContainsKey(cooldown.SpellId) ? Math.Max(0, level.MinCastInterval - ended) : 0;

                    Tally.Count($"cooldowns: {(cooldown.Rounds == expected ? "as the turns ended since the cast say" : $"off by {cooldown.Rounds - expected}")}");
                }

                break;
            }
        }
    }

    private FightMap MapOf(long mapId)
    {
        if (!_fightMaps.TryGetValue(mapId, out var map))
            _fightMaps[mapId] = map = FightMap.Of(_maps.Get(mapId));

        return map;
    }

    private void Settle(Cast cast, FightState after)
    {
        var foreseen = _effects.Foresee(cast.Map, cast.Fighters, cast.Caster, cast.Level, cast.Cell, cast.Critical);
        var alone = cast.TriggeredCasts is 0 ? "alone" : "with the casts it triggered";

        if (WeaponBlows.IsBlow(cast.Level))
        {
            var spent = -cast.Actions.Where(action => action.ActionId is 102 && action.ActionCase is GameActionFightEvent.ActionOneofCase.Points && action.Points.TargetId == cast.Caster.Id).Sum(action => action.Points.Delta);

            Tally.Count($"weapon's blows: AP spent {(spent == cast.Level.ApCost ? "as the weapon costs" : $"off by {spent - cast.Level.ApCost}")}");
            alone = "a weapon's blow";
        }

        States(cast, after);
        Damage(cast, foreseen, alone);
        Heals(cast, foreseen, alone);
        Pushes(cast, foreseen);
        Points(cast, foreseen, alone);
        Buffs(cast, foreseen);
        Moves(cast, foreseen);
    }

    private void Damage(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, string alone)
    {
        var dealt = new Dictionary<(long Target, EffectElement Element), (int Total, bool Capped)>();

        foreach (var action in cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.Element >= 0))
        {
            var lost = action.LifeLost;
            var key = (lost.TargetId, (EffectElement)lost.Element);
            var held = dealt.GetValueOrDefault(key);
            var life = cast.Lives.TryGetValue(lost.TargetId, out var before) ? before.Life : int.MaxValue;
            var total = held.Total + lost.Loss + (lost.HasShieldLoss ? lost.ShieldLoss : 0);

            dealt[key] = (total, held.Capped || total >= life);
        }

        var expected = new Dictionary<(long Target, EffectElement Element), LifeRange>();

        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage))
        {
            var key = (effect.TargetId, effect.Element);
            var held = expected.GetValueOrDefault(key);
            var surely = effect.IsCertain && !effect.IsRandom;

            expected[key] = new LifeRange(held.Min + (surely ? effect.Value.Min : 0), held.Max + effect.Value.Max);
        }

        foreach (var key in dealt.Keys.Union(expected.Keys))
        {
            var (total, capped) = dealt.GetValueOrDefault(key);

            if (!expected.TryGetValue(key, out var range))
            {
                Tally.Count($"damage, {alone}: dealt, not foreseen");
                _details?.WriteLine($"damage not foreseen: spell {cast.Level.SpellId} level {cast.Level.Id} element {key.Element}");

                continue;
            }

            if (!dealt.ContainsKey(key))
            {
                Tally.Count($"damage, {alone}: {(range.Min > 0 ? "foreseen, not dealt" : "maybe, not dealt")}");

                continue;
            }

            var outcome = Outcome(range, total, capped);

            Tally.Count($"damage, {alone}: {outcome}");

            if (cast.TriggeredCasts is 0)
                Tally.Count($"damage alone, by element: {key.Element}, {outcome}");

            if (outcome is "below" or "above")
                _details?.WriteLine($"damage {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id}{(cast.Critical ? " critical" : "")} element {key.Element}: {total} against {range.Min}..{range.Max}, {alone}; {Why(cast, foreseen, key.Target, key.Element)}");
        }
    }

    private string Why(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, long targetId, EffectElement element)
    {
        var caster = cast.Caster;
        var target = cast.Fighters.First(fighter => fighter.Id == targetId);
        var stat = EffectFormulas.CharacteristicOf(element);
        var offset = element is EffectElement.Neutral ? 4 : (int)element - 1;
        var rolls = string.Join(" ", _blows.Spells.EffectsOf(cast.Level).ToArray().Where(effect => foreseen.Any(found => found.TargetId == targetId && found.EffectId == effect.EffectId && found.Element == element))
            .Select(effect => $"e{effect.EffectId}:{effect.DiceNum}-{effect.DiceSide}/{(char)effect.Zone.Shape}{effect.Zone.Size},{effect.Zone.MinSize},{effect.Zone.DamageDecreaseStepPercent}x{effect.Zone.MaxDamageDecreaseApplyCount} r{effect.Random} g{effect.Group} t{_blows.Spells.TriggersOf(effect)}"));
        var changed = string.Join(",", caster.Characteristics.Where(pair => pair.Key is >= 100 and <= 160 && pair.Value is not 100 and not 0).Select(pair => $"{pair.Key}={pair.Value}"));
        var targetChanged = string.Join(",", target.Characteristics.Where(pair => pair.Key is >= 100 and <= 160 && pair.Value is not 100 and not 0).Select(pair => $"{pair.Key}={pair.Value}"));

        return $"caster {(caster.IsCharacter ? "character" : caster.IsSummon ? "summon" : "monster")} lvl {caster.Level} c={caster.Characteristic(stat)} p={caster.Characteristic(25)} all={caster.Characteristic(16)} el={caster.Characteristic(88 + offset)} crit={caster.Characteristic(86)} [{changed}] mods {caster.Modifiers.Length}; "
            + $"all caster [{string.Join(",", caster.Characteristics.Where(pair => pair.Value != 0).Select(pair => $"{pair.Key}={pair.Value}"))}] losses [{string.Join(",", cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.TargetId == targetId && action.LifeLost.Element == (int)element).Select(action => $"{action.ActionId}:{action.LifeLost.Loss}+{action.LifeLost.ShieldLoss}"))}] "
            + $"caster buffs [{string.Join(",", cast.Held.GetValueOrDefault(caster.Id).Select(buff => $"{buff.ActionId}/{buff.Kind}:{buff.Value}"))}] target buffs [{string.Join(",", cast.Held.GetValueOrDefault(targetId).Select(buff => $"{buff.ActionId}/{buff.Kind}:{buff.Value}"))}] "
            + $"target {(target.IsCharacter ? "character" : target.IsSummon ? "summon" : "monster")} res%={target.Characteristic(33 + offset)} fixed={target.Characteristic(54 + offset)} [{targetChanged}] away {(target.Cell < 0 ? -1 : MapGeometry.Distance(cast.Cell, target.Cell))} melee {(caster.Cell >= 0 && target.Cell >= 0 ? MapGeometry.Distance(caster.Cell, target.Cell) : -1)}; {rolls}";
    }

    private void Heals(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, string alone)
    {
        var healed = new Dictionary<long, (int Total, bool Capped)>();

        foreach (var action in cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeGained))
        {
            var gained = action.LifeGained;
            var held = healed.GetValueOrDefault(gained.TargetId);
            var missing = cast.Lives.TryGetValue(gained.TargetId, out var before) ? before.MaxLife - before.Life : int.MaxValue;
            var total = held.Total + gained.Amount;

            healed[gained.TargetId] = (total, held.Capped || total >= missing);
        }

        var expected = new Dictionary<long, LifeRange>();

        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Heal or EffectKind.FixedHeal or EffectKind.PercentHeal))
        {
            var held = expected.GetValueOrDefault(effect.TargetId);
            var surely = effect.IsCertain && !effect.IsRandom;

            expected[effect.TargetId] = new LifeRange(held.Min + (surely ? effect.Value.Min : 0), held.Max + effect.Value.Max);
        }

        // A steal heals its caster half the life it took, rounded down, of each target (D195).
        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Steal).DistinctBy(effect => (effect.TargetId, effect.Element)))
        {
            var taken = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.TargetId == effect.TargetId && action.LifeLost.Element == (int)effect.Element).Sum(action => action.LifeLost.Loss);
            var held = expected.GetValueOrDefault(cast.Caster.Id);

            expected[cast.Caster.Id] = new LifeRange(held.Min + (taken / 2), held.Max + (taken / 2));
            Tally.Count($"steals: foreseen, {(taken > 0 ? "life taken" : "no life taken")}");
        }

        foreach (var target in healed.Keys.Union(expected.Keys))
        {
            var (total, capped) = healed.GetValueOrDefault(target);

            if (!expected.TryGetValue(target, out var range))
            {
                Tally.Count($"heals, {alone}: healed, not foreseen");

                continue;
            }

            if (!healed.ContainsKey(target))
            {
                var whole = cast.Lives.TryGetValue(target, out var lives) && lives.Life >= lives.MaxLife;

                Tally.Count($"heals, {alone}: {(whole ? "foreseen, the target whole" : range.Min > 0 ? "foreseen, not healed" : "maybe, not healed")}");

                if (!whole && range.Min > 0)
                    _details?.WriteLine($"heal not announced: spell {cast.Level.SpellId} level {cast.Level.Id}, {(target == cast.Caster.Id ? "the caster" : "another")}");

                continue;
            }

            var outcome = Outcome(range, total, capped);

            Tally.Count($"heals, {alone}: {outcome}");

            if (outcome is "below" or "above")
                _details?.WriteLine($"heal {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id}: {total} against {range.Min}..{range.Max}, {alone}");
        }
    }

    private void Pushes(Cast cast, IReadOnlyList<ForeseenEffect> foreseen)
    {
        foreach (var push in foreseen.Where(effect => effect.IsImmediate && effect.IsCertain && effect.Action.Kind is EffectKind.Push or EffectKind.Pull))
        {
            var slides = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Slide && action.Slide.TargetId == push.TargetId).ToList();
            var steps = slides.Sum(slide => Steps(slide.Slide.FromCell, slide.Slide.ToCell));
            var cells = push.Value.Min;
            var kind = push.Action.Kind is EffectKind.Push ? "push" : "pull";
            var died = cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Death && action.Death.TargetId == push.TargetId);

            if (died && slides.Count is 0)
            {
                Tally.Count($"pushes and pulls: {kind}, the target dead first");

                continue;
            }

            Tally.Count($"pushes and pulls: {kind}, {(steps == cells ? "as far as foreseen" : steps < cells ? "stopped short" : "farther than foreseen")}");

            var damage = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.TargetId == push.TargetId && action.LifeLost.Element < 0).Sum(action => action.LifeLost.Loss + (action.LifeLost.HasShieldLoss ? action.LifeLost.ShieldLoss : 0));

            if (push.Action.Kind is not EffectKind.Push || push.EffectId is not 5)
                continue;

            var target = cast.Fighters.FirstOrDefault(fighter => fighter.Id == push.TargetId);
            var level = cast.Caster.SummonerId is { } summoner && cast.Fighters.FirstOrDefault(fighter => fighter.Id == summoner) is { } owner ? owner.Level : cast.Caster.Level;

            if (target is null)
                continue;

            var formula = steps < cells ? EffectFormulas.PushDamage(cells - steps, cast.Caster, level, target) : 0;
            var capped = cast.Lives.TryGetValue(target.Id, out var before) && damage >= before.Life;

            Tally.Count($"push damage: {(formula == damage ? (damage is 0 ? "none, as foreseen" : "as foreseen") : capped && damage <= formula ? "as foreseen, capped by the target's life" : damage < formula ? "below" : "above")}");

            if (formula != damage && !capped)
                _details?.WriteLine($"push damage: spell {cast.Level.SpellId}, {cells} cells, {steps} moved: {damage} against {formula}; target {(target.IsCharacter ? "character" : "monster")} lvl {target.Level} props {target.MonsterProperties} states {string.Join(",", target.States)} res85 {target.Characteristic(85)} caster lvl {level} bonus84 {cast.Caster.Characteristic(84)} shares {string.Join(",", target.ReceivedDamageShares)} slides {cast.Actions.Count(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Slide)} hits80 {cast.Actions.Count(action => action.ActionId is 80)}");
        }
    }

    private void Points(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, string alone)
    {
        foreach (var (characteristic, lostId, dodgedId, name) in new[] { (1, 101, 308, "AP"), (23, 127, 309, "MP") })
        {
            var lost = cast.Actions.Where(action => action.ActionId == lostId && action.ActionCase is GameActionFightEvent.ActionOneofCase.Points).GroupBy(action => action.Points.TargetId).ToDictionary(group => group.Key, group => -group.Sum(action => action.Points.Delta));
            var dodged = cast.Actions.Where(action => action.ActionId == dodgedId && action.ActionCase is GameActionFightEvent.ActionOneofCase.PointsDodged).GroupBy(action => action.PointsDodged.TargetId).ToDictionary(group => group.Key, group => group.Sum(action => action.PointsDodged.Amount));
            var expected = foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Deboost && effect.Action.Characteristic == characteristic)
                .GroupBy(effect => effect.TargetId).ToDictionary(group => group.Key, group => new LifeRange(group.Sum(effect => effect.Value.Min), group.Sum(effect => effect.Value.Max)));

            foreach (var target in lost.Keys.Union(dodged.Keys).Union(expected.Keys))
            {
                var taken = lost.GetValueOrDefault(target);
                var escaped = dodged.GetValueOrDefault(target);

                if (!expected.TryGetValue(target, out var range))
                {
                    Tally.Count($"{name} removed, {alone}: removed, not foreseen");

                    continue;
                }

                Tally.Count($"{name} removed, {alone}: {(range.Contains(taken + escaped) ? (escaped > 0 ? "within, some dodged" : "within") : taken + escaped < range.Min ? "below" : "above")}");
            }
        }
    }

    private bool CastsSpells(in SpellLevel level)
    {
        foreach (var effect in _blows.Spells.EffectsOf(level))
        {
            if (_actions.Of(effect.EffectId).Kind is EffectKind.CastSpell && effect.Delay is 0 && (effect.Properties & SpellEffectProperties.ForClientOnly) is 0 && _blows.Spells.TriggersOf(effect) == "I")
                return true;
        }

        return false;
    }

    private void States(Cast cast, FightState after)
    {
        // The states each fighter holds once the cast's sequence ended, against those the AI imagines the cast leaves, set and
        // removed by its effects and the spells they cast at once (D229); and the life the cast took, imagined and announced.
        var imagined = _imagination.Cast(new ImaginedFight { Map = cast.Map, Fighters = [.. cast.Fighters], ActorId = cast.Caster.Id, Round = cast.Round, History = CastHistory.Empty }, cast.Level, cast.Cell);
        var casts = CastsSpells(cast.Level) ? "casting spells at once" : "casting none";
        int lostImagined = 0, lostAnnounced = 0;

        foreach (var before in cast.Fighters)
        {
            if (!after.Fighters.TryGetValue(before.Id, out var now) || imagined.Find(before.Id) is not { } thought)
                continue;

            lostImagined += Math.Max(0, before.Life - thought.Life);
            lostAnnounced += Math.Max(0, cast.Lives[before.Id].Life - now.Life);

            // A fighter that died lost its buffs with its life.
            if (!now.IsAlive)
                continue;

            var was = before.States.ToHashSet();
            var held = now.States.ToHashSet();
            var foreseen = thought.States.ToHashSet();
            var who = before.Id == cast.Caster.Id ? "the caster" : "another fighter";

            foreach (var state in was.Union(held).Union(foreseen))
            {
                var outcome = (was.Contains(state), held.Contains(state), foreseen.Contains(state)) switch
                {
                    (false, true, true) => "set as foreseen",
                    (false, true, false) => "set, not foreseen",
                    (false, false, true) => "foreseen set, not set",
                    (true, false, false) => "removed as foreseen",
                    (true, false, true) => "removed, not foreseen",
                    (true, true, false) => "foreseen removed, kept",
                    _ => null,
                };

                if (outcome is null)
                    continue;

                Tally.Count($"states a cast changed, {who}, {casts}: {outcome}");

                if (outcome is not ("set as foreseen" or "removed as foreseen"))
                    _details?.WriteLine($"state {state} {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id}, {who}, {casts}");
            }
        }

        Tally.Count($"life a cast took, {casts}: casts");
        Tally.Count($"life a cast took, {casts}: imagined", lostImagined);
        Tally.Count($"life a cast took, {casts}: announced", lostAnnounced);
    }

    private void Buffs(Cast cast, IReadOnlyList<ForeseenEffect> foreseen)
    {
        // A mask naming the caster in the zone only, c, with the caster outside it: the server buffs it or not (D226).
        foreach (var effect in _blows.Spells.EffectsOf(cast.Level))
        {
            var mask = _blows.Spells.TargetMaskOf(effect).Split(',');

            if (!mask.Contains("c") || mask.Contains("C") || _actions.Of(effect.EffectId).Kind is not (EffectKind.Boost or EffectKind.Deboost or EffectKind.StateSet or EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield))
                continue;

            var zone = ZoneCells.Of(effect.Zone, _blows.Spells.CellsOf(effect.Zone), cast.Map, CastRules.OccupiedBy(cast.Fighters), cast.Caster.Cell, cast.Cell);

            if (zone.Contains(cast.Caster.Cell))
                continue;

            var buffed = cast.Buffs.Any(pair => pair.Buff.TargetId == cast.Caster.Id && pair.Buff.EffectUid == effect.Uid);

            Tally.Count($"a mask naming the caster in its zone only, the caster outside: {(buffed ? "buffed" : "not buffed")}");
        }

        // The client's previews of buffs on the caster, which the server gives through a spell the cast triggers (D209).
        foreach (var effect in _blows.Spells.EffectsOf(cast.Level))
        {
            var kind = _actions.Of(effect.EffectId).Kind;

            if ((effect.Properties & SpellEffectProperties.ForClientOnly) is 0 || kind is not (EffectKind.Boost or EffectKind.Deboost or EffectKind.StateSet)
                || !_rules.MaskOf(effect).NamesTheCaster)
            {
                continue;
            }

            var characteristic = _actions.Of(effect.EffectId).Characteristic;
            var given = cast.Buffs.Where(pair => pair.Buff.TargetId == cast.Caster.Id && (pair.ActionId == effect.EffectId || (characteristic > 0 && _actions.Of(pair.ActionId).Characteristic == characteristic))).Select(pair => pair.Buff).ToList();

            if (given.Count is 0)
                _details?.WriteLine($"buff previewed, not given: spell {cast.Level.SpellId} effect {effect.EffectId} characteristic {characteristic}; given on the caster: {string.Join(", ", cast.Buffs.Where(pair => pair.Buff.TargetId == cast.Caster.Id).Select(pair => $"{pair.ActionId}/{_actions.Of(pair.ActionId).Characteristic}={pair.Buff.Value}"))}");
            var roll = EffectFormulas.Roll(effect);
            var outcome = given.Count is 0 ? "not given"
                : kind is EffectKind.StateSet ? given.Any(buff => buff.Parameter == effect.Value) ? "given as previewed" : "given, another state"
                : given.Any(buff => roll.Contains(Math.Abs(buff.Value))) ? "given as previewed" : "given, another value";

            Tally.Count($"buffs previewed for the client on the caster: {kind}, {outcome}");
        }

        var own = new HashSet<int>();

        foreach (var effect in _blows.Spells.EffectsOf(cast.Level))
            own.Add(effect.Uid);

        foreach (var effect in _blows.Spells.CriticalEffectsOf(cast.Level))
            own.Add(effect.Uid);

        foreach (var (actionId, buff) in cast.Buffs)
        {
            if (!own.Contains(buff.EffectUid))
            {
                Tally.Count("buffs: of an effect the cast triggered");

                continue;
            }

            var matching = foreseen.Where(effect => effect.TargetId == buff.TargetId && effect.EffectId == actionId).ToList();

            if (matching.Count is 0)
            {
                Tally.Count("buffs: of the level's effects, target not foreseen");
                _details?.WriteLine($"buff target not foreseen: spell {cast.Level.SpellId} level {cast.Level.Id} effect {actionId}, mask {_blows.Spells.TargetMaskOf(SourceOf(cast.Level, buff.EffectUid))}, target {(buff.TargetId == cast.Caster.Id ? "the caster" : "another")}");

                continue;
            }

            var effect = matching[0];
            var kind = effect.Action.Kind;
            var source = SourceOf(cast.Level, buff.EffectUid);
            var outcome = buff.Kind switch
            {
                FightBuffKind.State => buff.Parameter == source.Value ? "state as foreseen" : source.Value is 0 ? "state the effect leaves to the server" : "another state",
                FightBuffKind.SpellModifier => buff.Value == source.DiceNum && buff.Parameter == source.Value ? "spell modifier as foreseen" : "another spell modifier",
                FightBuffKind.Triggered => "triggered effect, kept",
                _ when kind is EffectKind.Boost or EffectKind.Deboost or EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield => matching.Any(found => found.Value.Contains(buff.Value)) ? $"{Name(kind)} within" : $"{Name(kind)} outside",
                _ => $"{kind}, kept",
            };

            Tally.Count($"buffs: {outcome}");

            if (outcome.EndsWith("outside", StringComparison.Ordinal) || outcome.StartsWith("another", StringComparison.Ordinal))
                _details?.WriteLine($"buff {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id} effect {actionId}: value {buff.Value} parameter {buff.Parameter} against {effect.Value.Min}..{effect.Value.Max}, the effect's value {source.Value}");

            var rounds = source.Duration;
            var end = buff.End is { Round: >= 0 } at ? at.Round : -1;

            Tally.Count($"buffs' ends: {(rounds < 0 || end < 0 ? (rounds < 0 && end < 0 ? "the whole fight, as its effect lasts" : rounds <= 0 && end < 0 ? "the whole fight, its effect lasting 0" : "other") : end == cast.Round + rounds && buff.End.Turn == cast.Turn ? "the cast's round and its effect's duration, at the caster's turn" : "rounds otherwise")}");
        }

        static string Name(EffectKind kind)
        {
            return kind switch
            {
                EffectKind.Boost => "boost",
                EffectKind.Deboost => "deboost",
                _ => "shield",
            };
        }
    }

    private SpellEffect SourceOf(in SpellLevel level, int uid)
    {
        foreach (var effect in _blows.Spells.EffectsOf(level))
        {
            if (effect.Uid == uid)
                return effect;
        }

        foreach (var effect in _blows.Spells.CriticalEffectsOf(level))
        {
            if (effect.Uid == uid)
                return effect;
        }

        return default;
    }

    private void Moves(Cast cast, IReadOnlyList<ForeseenEffect> foreseen)
    {
        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Teleport or EffectKind.Swap or EffectKind.Summon))
        {
            var happened = effect.Action.Kind switch
            {
                EffectKind.Teleport => cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Teleport && action.Teleport.TargetId == effect.TargetId),
                EffectKind.Swap => cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Swap),
                _ => cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Summons && action.Summons.Fighters is { } summons
                    && summons.Summons.Any(summon => summon.Entity?.Monster is { } monster && monster.MonsterId == SourceOf(cast.Level, EffectUidOf(cast.Level, effect.EffectId)).DiceNum)),
            };

            Tally.Count($"teleports, swaps and summons: {effect.Action.Kind}, {(happened ? "as foreseen" : "not announced")}");
        }
    }

    private int EffectUidOf(in SpellLevel level, int effectId)
    {
        foreach (var effect in _blows.Spells.EffectsOf(level))
        {
            if (effect.EffectId == effectId)
                return effect.Uid;
        }

        return 0;
    }

    /// <summary>A cast a fighter chose, the fight as it stood before it, and what the server announced until its sequence closed.</summary>
    private sealed class Cast
    {
        public required Combatant Caster { get; init; }

        public required IReadOnlyList<Combatant> Fighters { get; init; }

        public required FightMap Map { get; init; }

        public required SpellLevel Level { get; init; }

        public required int Cell { get; init; }

        public required bool Critical { get; init; }

        public required int Depth { get; init; }

        public required int Round { get; init; }

        public required int Turn { get; init; }

        public required Dictionary<long, (int Life, int MaxLife)> Lives { get; init; }

        public required Dictionary<long, ImmutableArray<Bot.Fights.FightBuff>> Held { get; init; }

        public List<GameActionFightEvent> Actions { get; } = [];

        public List<(int ActionId, FightBuff Buff)> Buffs { get; } = [];

        public int TriggeredCasts { get; set; }
    }
}
