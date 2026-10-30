using System.Collections.Frozen;
using D3B.Bot.Activities;
using D3B.Bot.Settings;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Fights;

/// <summary>A fight's strategy, the profile each character plays (D233): its settings as the app edits them, its presets, and the AI's weights it leads to (D234).</summary>
public static class Strategies
{
    /// <summary>The strategy's id in the host's catalogue, which its profiles name.</summary>
    public const string ProfileId = "strategy";

    // Offensive: the threat feared less and next turn's damage weighed more, measured with D212's replay of the turns.
    private static readonly TacticWeights _offensive = new() { Threat = 0.8, Approach = 1, Survival = 1.5 };

    /// <summary>Registers the strategy, as a kind of profile of its own, for the app to edit and the characters to play.</summary>
    /// <param name="services">The host's services.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddStrategy<StrategySettings>(ProfileId, StrategyTexts.Text("Name"), Describe);
    }

    /// <summary>Gets the weights the AI fights a turn with, as the strategy says.</summary>
    /// <param name="strategy">The character's strategy.</param>
    /// <param name="risky">Whether the fight was judged too risky as it started (D198).</param>
    /// <param name="life">The character's life now, as a share of its most, from 0 to 1.</param>
    /// <returns>The weights: the style's, prudent when the strategy asks it now, changed by the role.</returns>
    public static TacticWeights WeightsOf(StrategySettings strategy, bool risky, double life)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        var prudent = (risky && strategy.PrudentWhenRisky) || (strategy.PrudentUnder && life * 100 < strategy.PrudentBelow);
        var style = prudent ? StrategyStyle.Prudent : strategy.Style;
        var weights = style switch
        {
            StrategyStyle.Prudent => TacticWeights.Prudent,
            StrategyStyle.Offensive => _offensive,
            _ => TacticWeights.Default,
        };

        weights = strategy.Role switch
        {
            StrategyRole.Healer => weights with { Allies = 2, Approach = weights.Approach * 0.6 },
            StrategyRole.Protector => weights with { Allies = 1.5, Survival = weights.Survival * 0.6, Approach = weights.Approach * 1.25 },
            _ => weights,
        };

        return strategy.Preferred.Count == 0 ? weights : weights with { Preferred = strategy.Preferred.ToFrozenSet() };
    }

    /// <summary>Gets the spells the AI never casts for the character, its weapon's blow among them when the strategy strikes not (D220).</summary>
    /// <param name="strategy">The character's strategy.</param>
    /// <returns>The spells, by id.</returns>
    public static FrozenSet<int> LeftOut(StrategySettings strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return strategy.Weapon ? strategy.Excluded.ToFrozenSet() : strategy.Excluded.Append(WeaponBlows.SpellId).ToFrozenSet();
    }

    /// <summary>Gets where the character prefers to stand as the fight starts, as the AI places it.</summary>
    /// <param name="strategy">The character's strategy.</param>
    /// <returns>The placement.</returns>
    public static TacticPlacement PlacementOf(StrategySettings strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return strategy.Placement switch
        {
            StrategyPlacement.Near => TacticPlacement.NearEnemies,
            StrategyPlacement.Far => TacticPlacement.FarFromEnemies,
            _ => TacticPlacement.Judged,
        };
    }

    private static void Describe(ActivitySettingsBuilder<StrategySettings> settings)
    {
        settings.Section(StrategyTexts.Text("SectionStrategy"));
        settings.Choice(s => s.Role, StrategyTexts.Text("Role"), role => StrategyTexts.Text($"Role{role}")).Help(StrategyTexts.Text("RoleHelp"));
        settings.Choice(s => s.Style, StrategyTexts.Text("Style"), style => StrategyTexts.Text($"Style{style}")).Help(StrategyTexts.Text("StyleHelp"));
        settings.Flag(s => s.PrudentWhenRisky, StrategyTexts.Text("PrudentWhenRisky")).Help(StrategyTexts.Text("PrudentWhenRiskyHelp"));
        settings.Flag(s => s.PrudentUnder, StrategyTexts.Text("PrudentUnder"));
        settings.Number(s => s.PrudentBelow, StrategyTexts.Text("PrudentBelow"), 5, 95)
            .Percent()
            .VisibleWhen(s => s.PrudentUnder);

        settings.Section(StrategyTexts.Text("SectionSpells"));
        settings.Spells(s => s.Preferred, StrategyTexts.Text("Preferred")).Help(StrategyTexts.Text("PreferredHelp"));
        settings.Spells(s => s.Excluded, StrategyTexts.Text("Excluded"))
            .Excluding()
            .Help(StrategyTexts.Text("ExcludedHelp"));
        settings.Flag(s => s.Weapon, StrategyTexts.Text("Weapon"));

        settings.Section(StrategyTexts.Text("SectionPlacement"));
        settings.Choice(s => s.Placement, StrategyTexts.Text("Placement"), placement => StrategyTexts.Text($"Placement{placement}")).Help(StrategyTexts.Text("PlacementHelp"));

        settings.Section(StrategyTexts.Text("SectionFight"));
        settings.Flag(s => s.Closed, StrategyTexts.Text("Closed")).Help(StrategyTexts.Text("ClosedHelp"));
        settings.Flag(s => s.NoSpectators, StrategyTexts.Text("NoSpectators"));
        settings.Flag(s => s.OutOfActivity, StrategyTexts.Text("OutOfActivity")).Help(StrategyTexts.Text("OutOfActivityHelp"));

        settings.Preset("prudent", StrategyTexts.Text("PresetPrudent"), s =>
        {
            s.Style = StrategyStyle.Prudent;
            s.PrudentUnder = true;
            s.PrudentBelow = 50;
        });
        settings.Preset("offensive", StrategyTexts.Text("PresetOffensive"), s => s.Style = StrategyStyle.Offensive);
        settings.Preset("healer", StrategyTexts.Text("PresetHealer"), s =>
        {
            s.Role = StrategyRole.Healer;
            s.Placement = StrategyPlacement.Far;
        });
        settings.Preset("protector", StrategyTexts.Text("PresetProtector"), s =>
        {
            s.Role = StrategyRole.Protector;
            s.Placement = StrategyPlacement.Near;
        });
    }
}
