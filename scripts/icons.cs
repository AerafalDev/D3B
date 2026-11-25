#!/usr/bin/env dotnet
#:package System.CommandLine
#:include icons/IconsCommand.cs
#:include icons/LucideIcon.cs
#:include icons/SvgPath.cs

using System.Globalization;
using D3B.Scripts.Icons;

// Takes the Lucide icons the app draws, as the next line draws them, from lucide-icons/lucide at a pinned version, and writes
// them for Avalonia in src/D3B.App/Themes/Icons.axaml under the app's own keys (D331, D532). The repository's license is
// checked: unless it is Lucide's ISC license, nothing is written.
// Usage: dotnet run --file scripts/icons.cs -- [--version <version>] [--output <file>] [--check]; --help describes the options.
// Exit code: 0 once written, or with --check when the file holds what the tool would write; 1 when the license is not
// Lucide's, an icon's drawing is beyond what the tool reads, or --check finds the file different; 2 when a file cannot be
// fetched.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return await IconsCommand.Create().Parse(args).InvokeAsync();
