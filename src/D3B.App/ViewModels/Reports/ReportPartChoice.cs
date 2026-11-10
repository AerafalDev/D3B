using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Reports;

namespace D3B.App.ViewModels.Reports;

/// <summary>One part of a report as the preview lists it: what it is, its size, what it says, and the checkbox that keeps it out (D410).</summary>
internal sealed partial class ReportPartChoice : ObservableObject
{
    /// <summary>Gets the part's title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the line under the title: what it holds.</summary>
    public required string Detail { get; init; }

    /// <summary>Gets the size of the part, as text.</summary>
    public required string Size { get; init; }

    /// <summary>Gets the part's text as it goes, for the journal, the trace and the message; null for a part shown by structure.</summary>
    public string? Preview { get; init; }

    /// <summary>Gets whether the part cannot be unchecked: the capture of a report of the game.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Gets the files the part makes in the report.</summary>
    public required IReadOnlyList<ReportPart> Files { get; init; }

    /// <summary>Gets whether the part has a text to show.</summary>
    public bool HasPreview => Preview is not null;

    /// <summary>Gets whether the checkbox can be changed.</summary>
    public bool CanChange => !IsRequired;

    /// <summary>Gets or sets whether the part goes in the report.</summary>
    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = true;

    partial void OnIsIncludedChanged(bool value)
    {
        // A required part cannot leave.
        if (IsRequired && !value)
            IsIncluded = true;
    }
}
