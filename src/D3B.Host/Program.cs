using D3B.Host;
using Microsoft.Extensions.Hosting;

var bot = BotHost.CreateBuilder(args);

using var host = bot.Build();

BotHost.LogComposition(host, bot.Modules);

await host.RunAsync();
