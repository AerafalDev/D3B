namespace D3B.Bot.Runtime;

/// <summary>A condition someone waits for, tried by the bot's loop.</summary>
internal sealed class BotWaiter
{
    private readonly Func<BotActor, bool> _condition;
    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;

    public Task Task => _held.Task;

    public bool IsDone => _held.Task.IsCompleted;

    public BotWaiter(Func<BotActor, bool> condition, CancellationToken cancellationToken)
    {
        _condition = condition;
        _registration = cancellationToken.Register(() => _held.TrySetCanceled(cancellationToken));
    }

    public void Try(BotActor bot)
    {
        if (IsDone)
            return;

        bool held;

        try
        {
            held = _condition(bot);
        }
        catch (Exception exception)
        {
            Fail(exception);

            return;
        }

        if (held && _held.TrySetResult())
            _registration.Dispose();
    }

    public void Fail(Exception exception)
    {
        if (_held.TrySetException(exception))
            _registration.Dispose();
    }
}
