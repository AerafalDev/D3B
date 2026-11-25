using D3B.Bot.Data;
using D3B.Bot.Dungeons;
using D3B.Bot.Fighting;
using D3B.Bot.Groups;
using D3B.Bot.Harvesting;
using D3B.Bot.Network;
using D3B.Bot.Protocol;
using D3B.Bot.Runtime;
using D3B.Bot.Transfers;
using D3B.Bot.Zaaps;
using D3B.Core.Modules;

namespace D3B.Bot;

/// <summary>The standard modules of a bot: network, protocol, data, world, movement, harvesting and bank, fighting, groups, the zaaps' discovery, the transfer between the bank and the guild's chest, and the dungeons.</summary>
/// <remarks>
/// D3B's own features are modules like any other, so whoever adds the group may disable one. Each roadmap step lists here
/// the modules it writes.
/// </remarks>
public sealed class DefaultModules : IModuleGroup
{
    /// <inheritdoc/>
    public void Build(ModuleGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.Add<DataModule>();
        group.Add<ProtocolModule>();
        group.Add<NetworkModule>();
        group.Add<BotsModule>();
        group.Add<HarvestingModule>();
        group.Add<FightingModule>();
        group.Add<GroupsModule>();
        group.Add<ZaapsModule>();
        group.Add<TransfersModule>();
        group.Add<DungeonsModule>();
    }
}
