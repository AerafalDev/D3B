namespace D3B.App.ViewModels.AppSettings;

/// <summary>A section of the settings' tab, in a player's order (D329, D516).</summary>
internal enum SettingsSection
{
    /// <summary>The app's own: its language, its layout.</summary>
    General,

    /// <summary>What every bot of the machine respects: the client's display time, the delays and the bank.</summary>
    Bots,

    /// <summary>Where D3B keeps what it knows and what the user makes.</summary>
    Files,

    /// <summary>What a player rarely changes: the captures of the game sessions and the advanced tools.</summary>
    Advanced,

    /// <summary>The app's version, its update and the licences.</summary>
    About,
}
