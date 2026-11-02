namespace D3B.Bot.Behaviors.Nodes;

/// <summary>A note in the bot's journal, to warn of what the behavior met.</summary>
internal sealed class WriteNote : InstantAction<NoteValues>
{
    public override void Apply(ActionContext context, NoteValues values)
    {
        context.Note(values.Text);
    }
}
