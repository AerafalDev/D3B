using static D3B.Engine.Tactics.JudgeTurns;

namespace D3B.Engine.Tactics;

/// <summary>How a fight looks for the fighter playing's side, from the damage tables: the turns each side takes to beat the other (D249).</summary>
internal sealed class JudgeOdds
{
    private readonly JudgeTables _tables;
    private readonly int _team;

    /// <summary>Initializes a new instance of the <see cref="JudgeOdds"/> class.</summary>
    /// <param name="tables">The judge's damage tables.</param>
    /// <param name="team">The fighter playing's side.</param>
    public JudgeOdds(JudgeTables tables, int team)
    {
        _tables = tables;
        _team = team;
    }

    /// <summary>Gets how the fight looks, every enemy striking every turn.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="knowledge">The points each fighter starts its turn with.</param>
    /// <returns>The odds.</returns>
    public FightOdds Of(ImaginedFight fight, FightKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);

        double ours = 0, theirs = 0, ourLife = 0, theirLife = 0;

        foreach (var fighter in fight.Fighters)
        {
            if (!fighter.IsAlive)
                continue;

            var known = _tables.IndexOf(fighter);
            var points = knowledge.TurnPoints.TryGetValue(fighter.Id, out var turn) ? turn.ActionPoints : fighter.ActionPoints;
            var best = 0.0;

            for (var other = 0; known >= 0 && other < _tables.Fighters.Count; other++)
            {
                if (_tables.DamageTable[known][other].Length > 0 && fight.Find(_tables.Fighters[other].Id) is { IsAlive: true })
                    best = Math.Max(best, _tables.DamageTable[known][other][Math.Clamp(points, 0, MostPoints)]);
            }

            if (fighter.Team == _team)
            {
                ours += best;
                ourLife += Effective(fighter);
            }
            else
            {
                theirs += best;
                theirLife += Effective(fighter);
            }
        }

        return new FightOdds(ours > 0 ? theirLife / ours : double.PositiveInfinity, theirs > 0 ? ourLife / theirs : double.PositiveInfinity);
    }

    /// <summary>Gets how the fight looks over its course: the enemies die one after another, each dealing its blows only while it lives.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="knowledge">The points each fighter starts its turn with.</param>
    /// <returns>The odds.</returns>
    public FightOdds OverTheFight(ImaginedFight fight, FightKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);

        double ours = 0, ourLife = 0;
        var enemies = new List<(double Damage, double Life)>();

        foreach (var fighter in fight.Fighters)
        {
            if (!fighter.IsAlive)
                continue;

            var known = _tables.IndexOf(fighter);
            var points = knowledge.TurnPoints.TryGetValue(fighter.Id, out var turn) ? turn.ActionPoints : fighter.ActionPoints;
            var best = 0.0;

            for (var other = 0; known >= 0 && other < _tables.Fighters.Count; other++)
            {
                if (_tables.DamageTable[known][other].Length > 0 && fight.Find(_tables.Fighters[other].Id) is { IsAlive: true })
                    best = Math.Max(best, _tables.DamageTable[known][other][Math.Clamp(points, 0, MostPoints)]);
            }

            if (fighter.Team == _team)
            {
                ours += best;
                ourLife += Effective(fighter);
            }
            else
            {
                enemies.Add((best, Math.Max(1, Effective(fighter))));
            }
        }

        if (ours <= 0)
        {
            var theirs = enemies.Sum(enemy => enemy.Damage);

            return new FightOdds(double.PositiveInfinity, theirs > 0 ? ourLife / theirs : double.PositiveInfinity);
        }

        enemies.Sort((a, b) => (b.Damage / b.Life).CompareTo(a.Damage / a.Life));

        var rate = enemies.Sum(enemy => enemy.Damage);
        double time = 0, taken = 0, lost = double.PositiveInfinity;

        foreach (var (damage, life) in enemies)
        {
            var span = life / ours;

            if (double.IsPositiveInfinity(lost) && rate > 0 && taken + (rate * span) >= ourLife)
                lost = time + ((ourLife - taken) / rate);

            taken += rate * span;
            time += span;
            rate -= damage;
        }

        return new FightOdds(time, lost);
    }
}
