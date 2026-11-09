#!/usr/bin/env dotnet
#:include shared/HookDecision.cs
#:include shared/HookInput.cs
#:include shared/HookResult.cs
#:include shared/HookVerdict.cs
#:include shared/ProjectPath.cs
#:include shared/WriteGuard.cs

using D3B.Hooks;

// PreToolUse on Edit, Write and NotebookEdit: refuses to write what is extracted from the official client, and the
// references in vendor/. The logic is in shared/WriteGuard.cs, with its tests.
var input = HookInput.ReadStandardInput();
var path = input.FilePath is null ? null : ProjectPath.Relative(input.ProjectDirectory(), input.FilePath);
var refusal = path is null ? null : WriteGuard.Refusal(path);
return HookResult.Report(refusal is null ? null : HookVerdict.Deny(refusal), Console.Out, Console.Error);
