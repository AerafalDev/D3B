#!/usr/bin/env dotnet
#:include shared/HookInput.cs
#:include shared/ProcessResult.cs
#:include shared/ProcessRunner.cs
#:include shared/TurnCheck.cs

using D3B.Hooks;

// Stop: builds the solution when the turn left changed code, since warnings are errors and the analyzers hold the style.
// It builds again only when a file changed, so that Claude's fix is checked and a repeated turn is not; it lets a build
// through that fails only because the app running from this checkout holds its files.
const int MaxLines = 60;

var input = HookInput.ReadStandardInput();
var root = input.ProjectDirectory();
var status = ProcessRunner.Run("git", ["status", "--porcelain", "--untracked-files=all"], root);
var changed = TurnCheck.ChangedFiles(status.Output);
if (!TurnCheck.NeedsBuild(changed))
{
    return 0;
}

var stamp = Path.Combine(root, "artifacts", "claude", "last-build.txt");
var fingerprint = TurnCheck.Fingerprint(root, changed);
if (!TurnCheck.ShouldBuild(File.Exists(stamp) ? File.ReadAllText(stamp) : null, fingerprint, input.StopHookActive))
{
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
var build = ProcessRunner.Run("dotnet", ["build", "--nologo", "-v", "q", "-clp:ErrorsOnly"], root);
if (build.ExitCode == 0)
{
    File.WriteAllText(stamp, TurnCheck.GreenStamp(fingerprint));
    return 0;
}

if (TurnCheck.IsOnlyLockedFiles(build.Output))
{
    Console.Error.WriteLine("The build could not replace files another process holds, probably the app running from this checkout; it was not checked.");
    return 0;
}

File.WriteAllText(stamp, TurnCheck.RedStamp(fingerprint));
Console.Error.WriteLine($"The build fails; fix it before ending the turn:{Environment.NewLine}{TurnCheck.Excerpt(build.Output, MaxLines)}");
return 2;
