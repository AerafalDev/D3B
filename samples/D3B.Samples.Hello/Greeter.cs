using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Samples.Hello;

/// <summary>Greets when the host starts.</summary>
internal sealed partial class Greeter : IHostedService
{
    private readonly ILogger _log;

    public Greeter(ILogger<Greeter> log)
    {
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Greet();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hello from a plugin")]
    private partial void Greet();
}
