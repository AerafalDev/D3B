using D3B.Extractor.Texts;

namespace D3B.Extractor.Items;

/// <summary>A job, such as the lumberjack, as <c>JobData</c> holds it.</summary>
internal sealed class Job
{
    public required int Id { get; init; }

    public required Text Name { get; init; }

    /// <summary>Gets its icon's number, -1 for none (job 1, Base, on 3.7.13.17).</summary>
    public required int IconId { get; init; }
}
