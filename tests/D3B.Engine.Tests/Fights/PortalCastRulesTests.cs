using System.Collections.Immutable;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

/// <summary>The rules of a cast on an open portal, as the references check it (D466): the portal from the caster, its target from the exit.</summary>
public sealed class PortalCastRulesTests
{
    private static readonly SpellCatalog _spells = Spells();
    private static readonly CastRules _rules = new(_spells, States());
    private static readonly Combatant _caster = Caster(Off(0, 0));

    // Cast on the portal at (2, 0), two cells from the caster, a cast comes out of (2, 8) onto (4, 8).
    private static readonly ImmutableArray<CellMark> _portals = [Portal(1, Off(2, 0)), Portal(2, Off(2, 8))];

    private static CellMark Portal(int id, int cell)
    {
        return new CellMark(id, 1, 0, CellMarkKind.Portal, 2, 44338, cell, [cell], 1_001);
    }

    [Fact]
    public void ACastOnAnOpenPortalNeedsWhatItsLevelAsksOnTheCellItComesOutOn()
    {
        var there = Enemy(-1, Off(4, 8));

        Assert.Equal(CastRefusal.None, Check(50, Off(2, 0), [_caster, there]));
        Assert.Equal(CastRefusal.CellNotTaken, Check(50, Off(2, 0), [_caster, Enemy(-1, Off(5, 8))]));
        Assert.Equal(CastRefusal.CellNotTaken, Check(50, Off(2, 0), [_caster, there], marks: []));
    }

    [Fact]
    public void ACastThroughAPortalNeedsTheLineOfSightFromTheExitToItsTarget()
    {
        Assert.Equal(CastRefusal.None, Check(10, Off(2, 0), [_caster, Enemy(-1, Off(4, 8))]));
        Assert.Equal(CastRefusal.NoLineOfSight, Check(10, Off(2, 0), [_caster, Enemy(-1, Off(4, 8))], map: WithPillars(Off(3, 8))));
    }

    [Fact]
    public void ALevelThatForbidsTheWayThroughPortalsOrClosesThemIsCastOnThePortalsCellItself()
    {
        var there = Enemy(-1, Off(4, 8));
        var forbidden = Level(_spells, 50) with { Properties = Level(_spells, 50).Properties | SpellLevelProperties.PortalProjectionForbidden };

        Assert.Equal(CastRefusal.CellNotTaken, _rules.Check(Open(), [_caster, there], _portals, _caster, forbidden, Off(2, 0), CastHistory.Empty, 1));
        Assert.Null(_rules.Passage(_portals, [_caster, there], _caster, forbidden, Off(2, 0)));
        Assert.Equal(new PortalPassage(Off(2, 0), Off(2, 8), Off(4, 8), 16), _rules.Passage(_portals, [_caster, there], _caster, Level(_spells, 50), Off(2, 0)));
    }

    [Fact]
    public void ALevelThatNeedsACellWithNoPortalIsRefusedOnAPortalsCell()
    {
        var portail = Level(_spells, 20) with { Properties = Level(_spells, 20).Properties | SpellLevelProperties.NeedCellWithoutPortal };
        ImmutableArray<CellMark> alone = [Portal(1, Off(2, 0))];

        Assert.Equal(CastRefusal.CellHasPortal, _rules.Check(Open(), [_caster], alone, _caster, portail, Off(2, 0), CastHistory.Empty, 1));
        Assert.Equal(CastRefusal.None, _rules.Check(Open(), [_caster], alone, _caster, portail, Off(3, 0), CastHistory.Empty, 1));
    }

    private static CastRefusal Check(int spell, int cell, Combatant[] fighters, FightMap? map = null, ImmutableArray<CellMark>? marks = null)
    {
        return _rules.Check(map ?? Open(), fighters, marks ?? _portals, _caster, Level(_spells, spell), cell, CastHistory.Empty, 1);
    }
}
