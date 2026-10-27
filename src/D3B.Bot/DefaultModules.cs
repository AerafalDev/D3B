using D3B.Core.Modules;

namespace D3B.Bot;

/// <summary>The standard modules of a bot: network, protocol, data, world, movement, harvesting and bank.</summary>
/// <remarks>D3B's own features are modules like any other, so whoever adds the group may disable one.</remarks>
public sealed class DefaultModules : IModuleGroup
{
    /// <inheritdoc/>
    public void Build(ModuleGroupBuilder group)
    {
        // Empty until the roadmap steps that write these modules: each step lists its own here.
    }
}
