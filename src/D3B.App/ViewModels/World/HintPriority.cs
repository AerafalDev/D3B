using D3B.Data.Hints;

namespace D3B.App.ViewModels.World;

/// <summary>How useful a hint of the world map is to someone building a route, which decides what stays when pictures crowd one place (D414).</summary>
/// <remarks>
/// The client's categories mix: the zaaps share Transports with the boats, and the banks, the taverns and the passages share
/// Divers, so the graphic, which names the picture, is read before the category. The order is the lead's reckoning: the zaaps
/// a route teleports through, the banks it ends at, then dungeons and workshops, which the owner names, auction houses and
/// class temples, the other transports, the other places, and last the passages between areas, which only a close view needs.
/// </remarks>
internal static class HintPriority
{
    private const int ZaapGfx = 410;
    private const int BankGfx = 401;
    private const int FirstPassageGfx = 900;
    private const int LastPassageGfx = 903;

    /// <summary>Gets the priority of a hint.</summary>
    /// <param name="hint">The hint.</param>
    /// <returns>Its priority, from 10, the least useful, to 100.</returns>
    public static int Of(Hint hint)
    {
        if (hint.Gfx is ZaapGfx)
            return 100;

        if (hint.Gfx is BankGfx)
            return 90;

        if (hint.Gfx is >= FirstPassageGfx and <= LastPassageGfx)
            return 10;

        return hint.CategoryId switch
        {
            6 => 80,
            3 => 70,
            2 => 60,
            1 => 50,
            9 => 40,
            _ => 30,
        };
    }
}
