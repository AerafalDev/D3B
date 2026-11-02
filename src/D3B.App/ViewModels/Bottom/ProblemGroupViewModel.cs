namespace D3B.App.ViewModels.Bottom;

/// <summary>The problems of one origin, under its heading (D327).</summary>
/// <param name="Title">The origin, named.</param>
/// <param name="Problems">Its problems, errors first.</param>
internal sealed record ProblemGroupViewModel(string Title, IReadOnlyList<ProblemViewModel> Problems);
