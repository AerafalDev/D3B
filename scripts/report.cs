#!/usr/bin/env dotnet
#:package System.CommandLine
#:project ../src/D3B.Bot/D3B.Bot.csproj
#:include report/ReportCommand.cs

using System.Globalization;
using D3B.Scripts.Report;

// Unpacks a bug report (D410), a .d3breport zip from a player, as untrusted input: it checks the zip's bounds and every file's size and SHA-256
// against its manifest, prints the player's message first (D411) with its control characters replaced, describes the report and its capture by
// structure, unpacks it under artifacts/reports/<code>/ (git-ignored), and with --replay runs scripts/fights.cs on its capture against the
// pack of its client build. Nothing in a report is ever run. The capture holds the server's data: it is never committed (D24).
// Usage: dotnet run --file scripts/report.cs -- <report.d3breport> [--out <folder>] [--replay]; --help describes the options.
// Exit code: 0 when the report was read; 1 when it is no report or goes past a bound; 2 when the file is missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return ReportCommand.Create().Parse(args).Invoke();
