#!/usr/bin/env dotnet
#:package System.CommandLine
#:include docs/BrokenLink.cs
#:include docs/DocsCommand.cs
#:include docs/HeadingSlugs.cs
#:include docs/LinkChecker.cs

using System.Globalization;
using D3B.Scripts.Docs;

// Looks after the repository's Markdown files (D401): finds the links whose file or heading does not exist, so that a
// document moved or split keeps every reference to it.
// Usage: dotnet run --file scripts/docs.cs -- [check] [--root <dir>]; --help describes the options.
// Exit code: 0 when every link leads somewhere; 1 when some do not, each printed as file:line: target: reason; 2 when the
// files cannot be listed or read.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return DocsCommand.Create().Parse(args).Invoke();
