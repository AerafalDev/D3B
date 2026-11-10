using System.Globalization;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Reports;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using D3B.Storage;
using D3B.Storage.Accounts;
using D3B.Studio.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Reports;

/// <summary>Reads, from the running host, what a character's report is made of (D410).</summary>
internal static class ReportSourcesFactory
{
    /// <summary>Gets the folder reports are written in by default: <c>%LOCALAPPDATA%\D3B\reports</c>.</summary>
    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "reports");

    /// <summary>Lists the characters a report may be about: those online first, then those the app knows.</summary>
    /// <param name="characters">The characters of the window.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <returns>One choice a character.</returns>
    public static IReadOnlyList<ReportCharacterChoice> Choices(CharactersViewModel characters, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(localizer);

        string Detail(string level, bool online)
        {
            var known = level.Length > 0 ? localizer.Format("ReportCharacterDetail", level) : localizer["ReportCharacterNoLevel"];

            return $"{known}, {localizer[online ? "ReportCharacterOnline" : "ReportCharacterOffline"]}";
        }

        return
        [
            .. characters.Online.Select(bot => new ReportCharacterChoice(bot.Bot.Key.AccountId, bot.Bot.Key.CharacterId, bot.Name, Detail(bot.Level, true))),
            .. characters.Offline.Select(line => new ReportCharacterChoice(line.Key.AccountId, line.Key.CharacterId, line.Name, Detail(line.Level, false))),
        ];
    }

    /// <summary>Reads what a character's report is made of.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>.</param>
    /// <param name="choice">The character, or <see langword="null"/> for a report of the app.</param>
    /// <param name="choices">Every character a report may be about, whose names no text may hold.</param>
    /// <param name="preferences">What the app remembers, which holds the settings the player changed.</param>
    /// <param name="appVersion">The app's version.</param>
    /// <param name="folder">Where the report is written.</param>
    /// <returns>The sources, or <see langword="null"/> when the host does not run.</returns>
    public static ReportSources? Read(IServiceProvider? services, ReportCharacterChoice? choice, IReadOnlyList<ReportCharacterChoice> choices, AppPreferences? preferences, string appVersion, string folder)
    {
        ArgumentNullException.ThrowIfNull(choices);

        if (services is null)
            return null;

        var key = choice is null ? default : new D3B.Bot.Characters.CharacterKey(choice.AccountId, choice.Id);
        var bots = services.GetService<Bots>();
        var bot = choice is not null && bots is not null && bots.TryGet(key, out var found) ? found : null;
        var known = choice is null ? null : services.GetService<AccountBook>()?.Find(key);
        var relay = services.GetService<RelaySettings>();
        var games = relay?.CapturesDirectory is { } captures ? Path.Combine(captures, "game") : null;
        var library = services.GetService<AssetLibrary>();
        var contexts = services.GetService<IDbContextFactory<StorageContext>>();
        var build = services.GetService<ClientBuild>();

        return new ReportSources
        {
            CharacterId = choice?.Id ?? 0,
            CharacterName = choice?.Name ?? string.Empty,
            OtherNames = [.. choices.Where(other => other.Id != choice?.Id).Select(other => other.Name)],
            Level = known?.Level ?? bot?.State.Level,
            Breed = known?.BreedId,
            CapturePath = games is not null ? ReportCaptures.Find(games, bot?.ConnectionId) : null,
            Translation = services.GetService<ProtocolTranslation>(),
            Journal = bot?.Journal.All() ?? [],
            Trace = bot?.Trace.All() ?? [],
            Assets = library is not null && known?.Profile is { } profile ? ReportAssets.Parts(library, profile) : [],
            Knowledge = contexts is null ? null : token => ReportKnowledge.ReadAsync(contexts, token),
            Settings = preferences?.Host ?? new Dictionary<string, string?>(),
            App = appVersion,
            Client = build?.Version ?? "0",
            Pack = GamePack.FormatVersion.ToString(CultureInfo.InvariantCulture),
            Mapping = MappingFile.FormatVersion.ToString(CultureInfo.InvariantCulture),
            Folder = folder,
            Time = services.GetService<TimeProvider>() ?? TimeProvider.System,
        };
    }
}
