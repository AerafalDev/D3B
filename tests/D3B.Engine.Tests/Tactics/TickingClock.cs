namespace D3B.Engine.Tests.Tactics;

/// <summary>A clock that moves on by a step each time it is read, so that a search's time passes with what it does.</summary>
internal sealed class TickingClock : TimeProvider
{
    private readonly long _step;
    private long _now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public TickingClock(TimeSpan step)
    {
        _step = step.Ticks;
    }

    public override long GetTimestamp()
    {
        return _now += _step;
    }
}
