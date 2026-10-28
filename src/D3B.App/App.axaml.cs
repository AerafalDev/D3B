using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;

namespace D3B.App;

/// <summary>The desktop app: its window shows the bots of the host that runs in its process (D91), dark unless asked otherwise.</summary>
internal sealed partial class App : Application
{
    /// <summary>How often the window reads the host and its bots again: their state is a snapshot, read for nothing (D9).</summary>
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets the bots' host the window shows.</summary>
    public HostRunner? Host { get; init; }

    /// <summary>Configures what the app draws with, the same with a screen and without one.</summary>
    /// <param name="builder">The app's builder.</param>
    /// <returns>The builder.</returns>
    public static AppBuilder Configure(AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Inter, the typeface ShadUI's design comes from, the same on every machine.
        return builder
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
    }

    private static bool Refreshed(MainViewModel main)
    {
        main.Refresh();

        return true;
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Host is { } host)
        {
            var main = new MainViewModel(host, Localizer.Instance);

            main.Refresh();
            desktop.MainWindow = new MainWindow { DataContext = main };

            DispatcherTimer.Run(() => Refreshed(main), _refreshInterval);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
