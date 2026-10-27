#!/usr/bin/env dotnet
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:include release/GitHubCli.cs
#:include release/ReleaseCommand.cs
#:include unity/ClientInstall.cs

using System.Globalization;
using D3B.Scripts.Release;

// Publishes the pack of a client build as an asset of the release client-<version> (D24, D33), with its SHA-256 beside
// it: the release is created when it does not exist, and gains the assets when it lacks them. A published pack is never
// replaced.
// Usage: dotnet run --file scripts/release.cs -- [--client <dir> | --client-version <version>] [--packs <dir>]
// [--publish]; --help describes the options.
// Without --publish it changes nothing on GitHub: it checks the pack, writes its SHA-256 beside it, and says what it
// would run. It runs the GitHub CLI, gh, logged in to an account that can write to the repository.
// Exit code: 0 on success, when the release already holds this pack, and after a dry run; 1 when the pack is not one to
// publish, the release cannot take it, or gh failed; 2 when the client or the pack is missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return ReleaseCommand.Create().Parse(args).Invoke();
