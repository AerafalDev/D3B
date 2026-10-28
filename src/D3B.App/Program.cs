using Avalonia;
using D3B.App.Hosting;

namespace D3B.App;

/// <summary>Starts the bots' host, then the window; stops the host once the window is gone (D91).</summary>
internal static class Program
{
    /// <summary>Runs the app.</summary>
    /// <param name="args">The command-line arguments, read as the host's configuration, as <c>D3B.Host</c> reads them.</param>
    /// <returns>The app's exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        var host = new HostRunner(args);

        host.Start();

        try
        {
            return BuildAvaloniaApp(host).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Off the UI's thread, which no longer runs: the bots stop, and the clients get their configuration back (D35).
            Task.Run(host.StopAsync).GetAwaiter().GetResult();
        }
    }

    private static AppBuilder BuildAvaloniaApp(HostRunner host)
    {
        return App.Configure(AppBuilder.Configure(() => new App { Host = host }))
            .UsePlatformDetect()
            .LogToTrace();
    }
}
