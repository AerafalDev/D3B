namespace D3B.Bot.Reports;

/// <summary>What the report areas say of a report.</summary>
public static class ReportAreas
{
    /// <summary>Tells whether a report of an area cannot go without the capture: every area of the game, not the app (D410).</summary>
    /// <param name="area">The area.</param>
    /// <returns>Whether the capture is required.</returns>
    public static bool NeedsCapture(this ReportArea area)
    {
        return area is not ReportArea.App;
    }
}
