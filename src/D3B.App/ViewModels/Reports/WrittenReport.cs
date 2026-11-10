namespace D3B.App.ViewModels.Reports;

/// <summary>A report written in a folder, ready to be handed over (D410).</summary>
/// <param name="Code">The code to give on Discord.</param>
/// <param name="Path">The file.</param>
/// <param name="Size">Its size in bytes.</param>
internal sealed record WrittenReport(string Code, string Path, long Size);
