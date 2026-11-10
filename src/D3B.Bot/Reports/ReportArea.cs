namespace D3B.Bot.Reports;

/// <summary>What a bug report is about, one of the app's areas (D410).</summary>
public enum ReportArea
{
    /// <summary>Fights.</summary>
    Fights,

    /// <summary>Harvest and the bank.</summary>
    HarvestAndBank,

    /// <summary>Moving and maps.</summary>
    MovingAndMaps,

    /// <summary>NPCs and their dialogs.</summary>
    NpcsAndDialogs,

    /// <summary>The app itself, which has no game to capture.</summary>
    App,
}
