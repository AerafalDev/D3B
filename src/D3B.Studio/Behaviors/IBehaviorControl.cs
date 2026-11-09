namespace D3B.Studio.Behaviors;

/// <summary>What an action asks of the behavior's run beside the game: its variables, a note, a pause, a stop.</summary>
internal interface IBehaviorControl
{
    void Pause();

    void Stop();

    void Note(string text);
}
