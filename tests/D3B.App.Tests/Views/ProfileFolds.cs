using Avalonia.Threading;
using D3B.App.ViewModels;

namespace D3B.App.Tests.Views;

/// <summary>Opens the Advanced folds of the profile being edited, for a test that reads every setting on the screen (D415).</summary>
internal static class ProfileFolds
{
    public static void Open(MainViewModel main)
    {
        foreach (var section in main.Profiles.Editing!.Editor!.Sections)
            section.IsAdvancedOpen = true;

        Dispatcher.UIThread.RunJobs();
    }
}
