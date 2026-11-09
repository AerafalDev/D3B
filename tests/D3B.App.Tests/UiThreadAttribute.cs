using System.Collections.Concurrent;
using System.Reflection;
using Xunit.v3;

namespace D3B.App.Tests;

/// <summary>
/// Gives every test of the class the studio's UI thread rule: what a view model resumes after an await (its editors' planning,
/// a save, a creation) runs on one thread, one step at a time, and never beside the test's own steps.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class UiThreadAttribute : BeforeAfterTestAttribute
{
    private static readonly ConcurrentDictionary<IXunitTest, UiThreadContext> _contexts = new();

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        var context = new UiThreadContext();

        _contexts[test] = context;
        SynchronizationContext.SetSynchronizationContext(context);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (!_contexts.TryRemove(test, out var context))
            return;

        if (SynchronizationContext.Current == context)
            SynchronizationContext.SetSynchronizationContext(null);

        context.Dispose();
    }
}
