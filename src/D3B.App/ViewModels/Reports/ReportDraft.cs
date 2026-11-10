using System.Globalization;
using System.Text;
using D3B.App.Localization;
using D3B.Bot.Reports;
using D3B.Network.Captures;

namespace D3B.App.ViewModels.Reports;

/// <summary>A report prepared and not yet written: its parts, each of which the player may uncheck, the capture cleaned, and the code to give on Discord (D410, D411).</summary>
/// <remarks>
/// Nothing leaves the machine from here: <see cref="Write"/> makes one file in a folder. The capture of a report of the game is required: when it
/// cannot be had or cleaned, the draft says why and cannot be written, and the player cancels instead.
/// </remarks>
internal sealed class ReportDraft
{
    private readonly ReportSources _sources;
    private readonly CleanCapture? _capture;

    private ReportDraft(ReportSources sources, ReportArea area, string code, IReadOnlyList<ReportPartChoice> parts, ReportPartChoice? capture, ReportPartChoice? message, CleanCapture? clean, string? captureProblem)
    {
        _sources = sources;
        Area = area;
        Code = code;
        Parts = parts;
        Capture = capture;
        Message = message;
        _capture = clean;
        CaptureProblem = captureProblem;
    }

    /// <summary>Gets what the report is about.</summary>
    public ReportArea Area { get; }

    /// <summary>Gets the report's code.</summary>
    public string Code { get; }

    /// <summary>Gets every part the player can see and uncheck, the capture first, the message last.</summary>
    public IReadOnlyList<ReportPartChoice> Parts { get; }

    /// <summary>Gets the capture's part, or null when there is none.</summary>
    public ReportPartChoice? Capture { get; }

    /// <summary>Gets the message's part, or null when the player wrote none.</summary>
    public ReportPartChoice? Message { get; }

    /// <summary>Gets why the capture is missing, if it is.</summary>
    public string? CaptureProblem { get; }

    /// <summary>Gets whether the report can be written: a report of the game needs its capture, kept.</summary>
    public bool CanWrite => !Area.NeedsCapture() || Capture is { IsIncluded: true };

    /// <summary>Prepares a report: cleans the capture, writes the texts, reads what the bots learned.</summary>
    /// <param name="sources">What the character's report is made of.</param>
    /// <param name="area">What the report is about.</param>
    /// <param name="message">The player's own message, or null or empty for none.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="cancellationToken">Stops the preparation.</param>
    /// <returns>The draft.</returns>
    public static async Task<ReportDraft> PrepareAsync(ReportSources sources, ReportArea area, string? message, Localizer localizer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(localizer);

        var identity = new ReportIdentity(sources.CharacterId, sources.CharacterName, sources.OtherNames);
        var parts = new List<ReportPartChoice>();
        ReportPartChoice? capture = null;
        CleanCapture? clean = null;
        string? problem = null;

        if (area.NeedsCapture())
        {
            (clean, problem) = await CleanAsync(sources, localizer, cancellationToken).ConfigureAwait(false);

            if (clean is not null)
            {
                capture = new ReportPartChoice
                {
                    Title = localizer["ReportPartCapture"],
                    Detail = Describe(clean, localizer),
                    Size = SizeOf(clean.Bytes.Length),
                    IsRequired = true,
                    Files = [],
                };
                parts.Add(capture);
            }
        }

        var journal = ReportTexts.Journal(sources.Journal, identity);
        var trace = ReportTexts.Trace(sources.Trace, identity);

        parts.Add(Text(localizer["ReportPartJournal"], localizer.Format("ReportPartJournalDetail", sources.Journal.Count), ReportFiles.Journal, journal));
        parts.Add(Text(localizer["ReportPartTrace"], localizer.Format("ReportPartTraceDetail", sources.Trace.Count), ReportFiles.Trace, trace));

        if (sources.Assets.Count > 0)
        {
            parts.Add(new ReportPartChoice
            {
                Title = localizer["ReportPartAssets"],
                Detail = localizer.Format("ReportPartAssetsDetail", sources.Assets.Count),
                Size = SizeOf(sources.Assets.Sum(asset => asset.Data.Length)),
                Preview = Joined(sources.Assets),
                Files = sources.Assets,
            });
        }

        var settings = ReportTexts.Settings(sources.Settings);

        parts.Add(Text(localizer["ReportPartSettings"], localizer.Format("ReportPartSettingsDetail", sources.Settings.Count), ReportFiles.Settings, settings));

        if (sources.Knowledge is { } knowledge)
        {
            var learned = await knowledge(cancellationToken).ConfigureAwait(false);

            parts.Add(new ReportPartChoice
            {
                Title = localizer["ReportPartKnowledge"],
                Detail = localizer["ReportPartKnowledgeDetail"],
                Size = SizeOf(learned.Data.Length),
                Preview = Joined([learned]),
                Files = [learned],
            });
        }

        ReportPartChoice? messagePart = null;

        if (!string.IsNullOrEmpty(message))
        {
            messagePart = new ReportPartChoice { Title = localizer["ReportPartMessage"], Detail = localizer["ReportPartMessageDetail"], Size = SizeOf(Encoding.UTF8.GetByteCount(message)), Preview = message, Files = [] };
            parts.Add(messagePart);
        }

        return new ReportDraft(sources, area, ReportCode.Create(), parts, capture, messagePart, clean, problem);
    }

    /// <summary>Writes the report in the folder, with the parts kept: one file named after its code.</summary>
    /// <returns>The code and where the file is.</returns>
    /// <exception cref="InvalidOperationException">The capture a report of the game needs is missing or unchecked.</exception>
    public WrittenReport Write()
    {
        if (!CanWrite)
            throw new InvalidOperationException("A report of the game cannot go without its capture.");

        var kept = Parts.Where(part => part.IsIncluded).ToList();
        var content = new ReportContent
        {
            Code = Code,
            CreatedAt = _sources.Time.GetUtcNow(),
            App = _sources.App,
            Client = _sources.Client,
            Pack = _sources.Pack,
            Mapping = _sources.Mapping,
            Area = Area,
            Character = new ReportCharacter(_sources.CharacterId, _sources.Breed, _sources.Level),
            Message = Message is { IsIncluded: true } ? Message.Preview : null,
            Capture = Capture is { IsIncluded: true } ? _capture : null,
            Parts = [.. kept.SelectMany(part => part.Files)],
        };

        Directory.CreateDirectory(_sources.Folder);

        var path = Path.Combine(_sources.Folder, Code + ".d3breport");
        var partial = path + ".part";

        // Written beside, then moved: a write that fails leaves no file that a second try would meet, and none that looks whole.
        try
        {
            using (var stream = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                ReportWriter.Write(content, stream);

            File.Move(partial, path);
        }
        catch
        {
            File.Delete(partial);

            throw;
        }

        return new WrittenReport(Code, path, new FileInfo(path).Length);
    }

    private static async Task<(CleanCapture? Clean, string? Problem)> CleanAsync(ReportSources sources, Localizer localizer, CancellationToken cancellationToken)
    {
        if (sources.CapturePath is not { } path)
            return (null, localizer["ReportNoCapture"]);

        if (sources.Translation is not { } translation)
            return (null, localizer["ReportNoTranslation"]);

        try
        {
            // Read while the capture may still be written: the relay shares it for reading.
            return await Task.Run(
                () =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                    return ((CleanCapture?)new CaptureScrubber(translation).Clean(stream), (string?)null);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (CaptureScrubException exception)
        {
            return (null, localizer.Format("ReportCaptureRefused", exception.Message));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CaptureFormatException)
        {
            return (null, localizer.Format("ReportCaptureUnreadable", exception.Message));
        }
    }

    // The files a part holds, one after the other with their names, as the player reads them.
    private static string Joined(IReadOnlyList<ReportPart> files)
    {
        return string.Join("\n\n", files.Select(file => $"// {file.Name}\n{Encoding.UTF8.GetString(file.Data)}"));
    }

    private static ReportPartChoice Text(string title, string detail, string name, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        return new ReportPartChoice { Title = title, Detail = detail, Size = SizeOf(bytes.Length), Preview = text, Files = [new ReportPart(name, bytes)] };
    }

    // The capture by structure, never by content: how long, what was overwritten, and what stays.
    private static string Describe(CleanCapture clean, Localizer localizer)
    {
        using var reader = new CaptureReader(new MemoryStream(clean.Bytes));
        var records = reader.ReadAll().ToList();
        var last = records.Count > 0 ? records[^1].Offset : TimeSpan.Zero;
        var scrub = clean.Scrub;

        return localizer.Format("ReportPartCaptureDetail", records.Count.ToString("N0", localizer.Culture), last.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), scrub.Names, scrub.Secrets);
    }

    private static string SizeOf(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024d / 1024d:0.#} MB"),
            >= 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024d:0.#} KB"),
            _ => string.Create(CultureInfo.CurrentCulture, $"{bytes} B"),
        };
    }
}
