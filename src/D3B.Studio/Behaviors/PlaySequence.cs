using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Studio.Assets;

namespace D3B.Studio.Behaviors;

/// <summary>Plays a list of steps of the project as one step of another: its steps in order, once for each pass of the step (D420).</summary>
/// <remarks>
/// <para>
/// The list is compiled and walked by a visit of its own, the way a behavior's leaf is, so that its steps repeat, wait for a
/// condition, follow routes and play other lists as they do anywhere. The place it has reached outlives a cut, a fight or a bank
/// going on from where the list was (<see cref="SequenceRuns"/>), and the list's own repeat is left to the step that plays it.
/// </para>
/// <para>A list that plays itself, directly or through others, ends the run with a note rather than never ending.</para>
/// </remarks>
internal sealed class PlaySequence : ActionNode<SequenceValues>
{
    private static readonly Dictionary<string, JsonNode?> _noProfile = [];

    private readonly AssetLibrary _assets;
    private readonly NodeCatalog _nodes;
    private readonly SequenceRuns _runs;
    private readonly IServiceProvider _services;

    /// <summary>Initializes a new instance of the <see cref="PlaySequence"/> class.</summary>
    /// <param name="assets">The project, which holds the lists.</param>
    /// <param name="nodes">The kinds of nodes, which the steps of a list are.</param>
    /// <param name="runs">The places the lists have reached.</param>
    /// <param name="services">The host's services, which the activities a step makes are built from.</param>
    public PlaySequence(AssetLibrary assets, NodeCatalog nodes, SequenceRuns runs, IServiceProvider services)
    {
        _assets = assets;
        _nodes = nodes;
        _runs = runs;
        _services = services;
    }

    /// <inheritdoc/>
    public override async Task RunAsync(ActionContext context, SequenceValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("A sequence is played in a step, which has a bot to move.");

        if (_assets.Find<BehaviorAsset>(values.Sequence) is not { } behavior || BehaviorSequences.Read(behavior) is null)
        {
            Say(context, "SequenceMissing", values.Sequence);
            context.Stop();

            return;
        }

        if (behavior.States.IsEmpty)
            return;

        var key = bot.State.Key;

        if (!_runs.TryBegin(key, behavior.Id))
        {
            Say(context, "SequenceCycle", behavior.Name);
            context.Stop();

            return;
        }

        try
        {
            // What the step that plays it asks of its repeat is the step's; the list's own is not played.
            var compilation = BehaviorCompiler.Compile(behavior with { States = [behavior.States[0] with { Repeat = StepRepeat.Once }] }, _noProfile, _nodes, null);

            if (compilation.Behavior is not { States: [var state, ..] })
            {
                Say(context, "SequenceProblems", behavior.Name, string.Join(" ", compilation.Problems.Select(problem => problem.Detail)));
                context.Stop();

                return;
            }

            var visit = _runs.Of(key, behavior.Id, context.View.Started, () => new StateVisit(state, _services));
            var control = new Watch(context.Control);
            var actor = bot.Actor;

            Evaluation Now()
            {
                return new Evaluation(new BehaviorView(actor.State, actor.IsConnected, actor.LastEndedFight, actor.LastFightEnd, actor.Time.GetUtcNow(), context.View.Started), context.Variables!);
            }

            while (!visit.IsFinished)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await visit.StepAsync(bot, Now, control, () => control.IsHalted, _ => { }, cancellationToken).ConfigureAwait(false);

                // A pause or a stop in the list cuts this step as it cuts any: the run settles, and a resume plays the step again,
                // which goes on in the list from where it was. Returning would tell the step it was played to its end.
                if (control.IsHalted)
                    throw new OperationCanceledException(cancellationToken);

                await Task.Yield();
            }

            _runs.Forget(key, behavior.Id);
        }
        finally
        {
            _runs.End(key, behavior.Id);
        }
    }

    private static void Say(ActionContext context, string key, params object[] values)
    {
        var culture = CultureInfo.CurrentUICulture;

        context.Note(string.Format(culture, BehaviorTexts.Resources.GetString(key, culture) ?? key, values));
    }

    // Tells the list's steps' pauses and stops to the run, and remembers them so that the list stops walking.
    private sealed class Watch : IBehaviorControl
    {
        private readonly IBehaviorControl _run;

        public Watch(IBehaviorControl run)
        {
            _run = run;
        }

        public bool IsHalted { get; private set; }

        public void Pause()
        {
            IsHalted = true;
            _run.Pause();
        }

        public void Stop()
        {
            IsHalted = true;
            _run.Stop();
        }

        public void Note(string text)
        {
            _run.Note(text);
        }
    }
}
