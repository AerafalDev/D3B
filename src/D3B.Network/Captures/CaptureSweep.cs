namespace D3B.Network.Captures;

/// <summary>What <see cref="CaptureRetention.Sweep"/> did to a directory of captures.</summary>
/// <param name="Deleted">The captures deleted.</param>
/// <param name="DeletedBytes">Their size together.</param>
/// <param name="Kept">The captures left.</param>
/// <param name="KeptBytes">Their size together.</param>
public readonly record struct CaptureSweep(int Deleted, long DeletedBytes, int Kept, long KeptBytes);
