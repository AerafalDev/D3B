#!/usr/bin/env dotnet
#:include shared/GitGuard.cs
#:include shared/HookDecision.cs
#:include shared/HookInput.cs
#:include shared/HookResult.cs
#:include shared/HookVerdict.cs
#:include shared/ProcessResult.cs
#:include shared/ProcessRunner.cs
#:include shared/ShellCommand.cs
#:include shared/ShellInvocation.cs

using D3B.Hooks;

// PreToolUse on Bash and PowerShell: refuses a push to main, a forced push and git reset --hard, and asks the owner
// before any other push and before gh publishes or merges anything. The logic is in shared/GitGuard.cs, with its tests.
var input = HookInput.ReadStandardInput();
var branch = ProcessRunner.Run("git", ["branch", "--show-current"], input.ProjectDirectory()).Output.Trim();
return HookResult.Report(GitGuard.Check(input.Command ?? string.Empty, branch), Console.Out, Console.Error);
