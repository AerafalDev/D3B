#!/usr/bin/env dotnet
#:include shared/FormatTarget.cs
#:include shared/HookInput.cs
#:include shared/ProcessResult.cs
#:include shared/ProcessRunner.cs
#:include shared/ProjectFile.cs
#:include shared/ProjectPath.cs

using D3B.Hooks;

// PostToolUse on Edit and Write: fixes the whitespace of the C# file just written, so that dotnet format
// --verify-no-changes stays green. The style rules are the build's, which the Stop hook runs.
var input = HookInput.ReadStandardInput();
var root = input.ProjectDirectory();
var path = input.FilePath is null ? null : ProjectPath.Relative(root, input.FilePath);
var project = path is not null && FormatTarget.IsFormatted(path) ? ProjectFile.Find(root, path) : null;
if (path is null || project is null)
{
    return 0;
}

var result = ProcessRunner.Run("dotnet", ["format", "whitespace", project, "--no-restore", "--include", path], root);
if (result.ExitCode != 0)
{
    Console.Error.WriteLine($"dotnet format could not format {path}:{Environment.NewLine}{result.Output}");
}

return 0;
