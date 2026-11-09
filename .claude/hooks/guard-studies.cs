#!/usr/bin/env dotnet
#:include shared/HookDecision.cs
#:include shared/HookInput.cs
#:include shared/HookResult.cs
#:include shared/HookVerdict.cs
#:include shared/ProjectPath.cs
#:include shared/StudyGuard.cs

using D3B.Hooks;

// PreToolUse of the explorer agent only: it writes under artifacts/studies/ and its shell only reads, so that what it
// quotes from the client and from vendor/ stays out of Git. The logic is in shared/StudyGuard.cs, with its tests.
var input = HookInput.ReadStandardInput();
var refusal = input.Command is null
    ? StudyGuard.PathRefusal(input.FilePath is null ? null : ProjectPath.Relative(input.ProjectDirectory(), input.FilePath))
    : StudyGuard.ShellRefusal(input.Command);
return HookResult.Report(refusal is null ? null : HookVerdict.Deny(refusal), Console.Out, Console.Error);
