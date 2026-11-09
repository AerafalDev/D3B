namespace D3B.Hooks;

/// <summary>A guard's answer to a tool call it refuses or hands to the owner; a call it lets through has no verdict.</summary>
internal sealed class HookVerdict
{
    private HookVerdict(HookDecision decision, string reason)
    {
        Decision = decision;
        Reason = reason;
    }

    public HookDecision Decision { get; }

    public string Reason { get; }

    public static HookVerdict Deny(string reason)
    {
        return new HookVerdict(HookDecision.Deny, reason);
    }

    public static HookVerdict Ask(string reason)
    {
        return new HookVerdict(HookDecision.Ask, reason);
    }
}
