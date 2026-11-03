namespace D3B.App.ViewModels.AppSettings;

/// <summary>A section of the settings' tab (D329).</summary>
internal enum SettingsSection
{
    /// <summary>What every bot of the machine respects: the client's display time and the delays.</summary>
    Bots,

    /// <summary>How D3B stands between the client and the servers: the captures.</summary>
    Network,

    /// <summary>The app's own: its language and its layout.</summary>
    Appearance,

    /// <summary>Where D3B keeps what it knows and what the user makes.</summary>
    Files,

    /// <summary>The app's version, its update and the licences.</summary>
    About,
}
