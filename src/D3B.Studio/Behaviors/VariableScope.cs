namespace D3B.Studio.Behaviors;

/// <summary>How long a variable lives and who shares it, said in the open (D298).</summary>
public enum VariableScope
{
    /// <summary>The run: from its start to its end, the default.</summary>
    Run,

    /// <summary>The character: kept in D3B's database, read by every behavior it plays by the variable's name.</summary>
    Character,

    /// <summary>Shared by the host's bots, for step 10's groups; lost when the host stops.</summary>
    Shared,
}
