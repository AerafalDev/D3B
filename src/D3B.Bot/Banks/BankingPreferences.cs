using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Banks;

/// <summary>When the bots bank and whether they take a zaap to the bank: the machine's setting, which a profile may answer with its own (D405).</summary>
/// <remarks>
/// Kept as the host's configuration names them, <c>Banking:FullAt</c> and <c>Banking:UseZaaps</c>, and changed at once from the
/// app's settings: the activities read it at each step, so a share set while a bot plays applies to its next step. A profile
/// that asks for its own banking (<c>OwnBanking</c> in its settings) ignores it.
/// </remarks>
public sealed class BankingPreferences
{
    /// <summary>The setting naming the share of pods at which a bot banks.</summary>
    public const string FullAtKey = "Banking:FullAt";

    /// <summary>The setting naming whether a bot may take a zaap to the bank.</summary>
    public const string UseZaapsKey = "Banking:UseZaaps";

    /// <summary>The share of pods, in percent, the bots bank at when nothing says otherwise: what the profiles had before the setting was general.</summary>
    public const int DefaultFullAt = 90;

    /// <summary>The least share of pods a bot may bank at.</summary>
    public const int LeastFullAt = 50;

    /// <summary>The most share of pods a bot may bank at.</summary>
    public const int MostFullAt = 100;

    private volatile int _fullAt = DefaultFullAt;
    private volatile bool _useZaaps = true;

    /// <summary>Gets or sets the share of its pods, in percent, at which a character banks.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not from <see cref="LeastFullAt"/> to <see cref="MostFullAt"/>.</exception>
    public int FullAt
    {
        get => _fullAt;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, LeastFullAt);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MostFullAt);
            _fullAt = value;
        }
    }

    /// <summary>Gets or sets whether a character may take a zaap to go to the bank; else it walks.</summary>
    public bool UseZaaps
    {
        get => _useZaaps;
        set => _useZaaps = value;
    }

    /// <summary>Reads the preferences from a configuration, the defaults for what it does not set.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The preferences.</returns>
    /// <exception cref="InvalidOperationException">A value is not a share from 50 to 100, or not a boolean.</exception>
    public static BankingPreferences Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var preferences = new BankingPreferences();

        if (configuration[FullAtKey] is { Length: > 0 } share)
        {
            if (!int.TryParse(share, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) || percent is < LeastFullAt or > MostFullAt)
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{FullAtKey} is '{share}', which is not a share from {LeastFullAt} to {MostFullAt}."));

            preferences.FullAt = percent;
        }

        if (configuration[UseZaapsKey] is { Length: > 0 } zaaps)
        {
            if (!bool.TryParse(zaaps, out var use))
                throw new InvalidOperationException($"{UseZaapsKey} is '{zaaps}', which is not true or false.");

            preferences.UseZaaps = use;
        }

        return preferences;
    }

    /// <summary>Gets the configuration these preferences read back from: a value left as the default is not set.</summary>
    /// <returns>Each setting, with no value for one left as the default.</returns>
    public IReadOnlyDictionary<string, string?> ToConfiguration()
    {
        return new Dictionary<string, string?>
        {
            [FullAtKey] = FullAt == DefaultFullAt ? null : FullAt.ToString(CultureInfo.InvariantCulture),
            [UseZaapsKey] = UseZaaps ? null : bool.FalseString,
        };
    }

    /// <summary>Gets the share of pods a profile banks at.</summary>
    /// <param name="own">Whether the profile asks for its own banking.</param>
    /// <param name="profile">The profile's share.</param>
    /// <returns>The profile's share when it asks for its own, else the general one.</returns>
    public int FullAtOf(bool own, int profile)
    {
        return own ? profile : FullAt;
    }

    /// <summary>Gets whether a profile's characters may take a zaap to the bank.</summary>
    /// <param name="own">Whether the profile asks for its own banking.</param>
    /// <param name="profile">The profile's answer.</param>
    /// <returns>The profile's answer when it asks for its own, else the general one.</returns>
    public bool UseZaapsOf(bool own, bool profile)
    {
        return own ? profile : UseZaaps;
    }
}
