using System.Collections.Immutable;

namespace D3B.Bot.Behaviors;

/// <summary>A condition once compiled: what a state's entry, a rule or a branch reads.</summary>
internal abstract class CompiledCondition
{
    public static CompiledCondition Node(IConditionNode node, BoundValues values)
    {
        return new NodeHolds(node, values);
    }

    public static CompiledCondition All(ImmutableArray<CompiledCondition> conditions)
    {
        return new Joined(conditions, all: true);
    }

    public static CompiledCondition Any(ImmutableArray<CompiledCondition> conditions)
    {
        return new Joined(conditions, all: false);
    }

    public static CompiledCondition Not(CompiledCondition condition)
    {
        return new NotHolds(condition);
    }

    public abstract bool Holds(Evaluation evaluation);

    private sealed class NodeHolds : CompiledCondition
    {
        private readonly IConditionNode _node;
        private readonly BoundValues _values;

        public NodeHolds(IConditionNode node, BoundValues values)
        {
            _node = node;
            _values = values;
        }

        public override bool Holds(Evaluation evaluation)
        {
            try
            {
                return _node.Holds(evaluation.View, _values.Resolve(evaluation)!);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Read from the bot's loop too, which must not fail on a module's condition: one that throws does not hold.
                return false;
            }
        }
    }

    private sealed class Joined : CompiledCondition
    {
        private readonly ImmutableArray<CompiledCondition> _conditions;
        private readonly bool _all;

        public Joined(ImmutableArray<CompiledCondition> conditions, bool all)
        {
            _conditions = conditions;
            _all = all;
        }

        public override bool Holds(Evaluation evaluation)
        {
            foreach (var condition in _conditions)
            {
                if (condition.Holds(evaluation) != _all)
                    return !_all;
            }

            return _all;
        }
    }

    private sealed class NotHolds : CompiledCondition
    {
        private readonly CompiledCondition _condition;

        public NotHolds(CompiledCondition condition)
        {
            _condition = condition;
        }

        public override bool Holds(Evaluation evaluation)
        {
            return !_condition.Holds(evaluation);
        }
    }
}
