#!/usr/bin/env dotnet
#:package System.CommandLine
#:include icons/IconsCommand.cs
#:include icons/IconShape.cs
#:include icons/SvgIcon.cs
#:include icons/SvgPath.cs
#:include icons/SvgStyle.cs

using System.Globalization;
using D3B.Scripts.Icons;

// Takes the IntelliJ Platform's icons the app draws, the dark variants of platform/icons/src/expui in
// JetBrains/intellij-community at a pinned revision, and writes them for Avalonia in src/D3B.App/Themes/IntelliJIcons.axaml
// (D305, D331). Each file's own notice is checked: an icon whose file does not say it is under the Apache 2.0 license is
// refused, and nothing is written. JetBrains' products' logos are never among them.
// Usage: dotnet run --file scripts/icons.cs -- [--revision <sha>] [--output <file>] [--check]; --help describes the options.
// Exit code: 0 once written, or with --check when the file holds what the tool would write; 1 when an icon's notice is
// missing, its drawing is beyond what the tool reads, or --check finds the file different; 2 when an icon cannot be fetched.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return await IconsCommand.Create().Parse(args).InvokeAsync();
