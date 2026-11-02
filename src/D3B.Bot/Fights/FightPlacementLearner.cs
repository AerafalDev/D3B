using System.Runtime.CompilerServices;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Fights;

/// <summary>Teaches <see cref="FightPlacements"/> each fight's map and placement cells, on every game connection (9f3).</summary>
/// <remarks>
/// <para>
/// The map is the one the server last said the connection's character is on; the cells, the first the server gives once
/// a fight starts, as the fight's state reads them (<see cref="FightEvents"/>). A fight counts once, whatever the server
/// gives again while the fighters place.
/// </para>
/// <para>It sees the frames on the relay's read loops: what it keeps of a connection is that connection's alone, and goes with it.</para>
/// </remarks>
internal sealed class FightPlacementLearner : IGameObserver
{
    private const int Cells = 560;

    private readonly FightPlacements _placements;
    private readonly TimeProvider _time;
    private readonly ConditionalWeakTable<GameSession, Presence> _presences = [];

    public FightPlacementLearner(FightPlacements placements, TimeProvider time)
    {
        _placements = placements;
        _time = time;
    }

    public void OnSessionStarted(GameSession session)
    {
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (direction is not RelayDirection.FromServer || !frame.IsRead)
            return;

        var presence = _presences.GetOrCreateValue(session);

        lock (presence)
        {
            switch (frame.Content)
            {
                case MapCurrentEvent current:
                    presence.MapId = current.MapId;
                    break;
                case FightStartingEvent:
                    presence.Counted = false;
                    break;
                case FightPlacementPossiblePositionsEvent { Cells: { } cells } when !presence.Counted && presence.MapId is { } mapId:
                    presence.Counted = true;
                    _placements.Seen(mapId, cells.ChallengerCells.Where(IsCell), cells.DefenderCells.Where(IsCell), _time.GetUtcNow());
                    break;
            }
        }
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < Cells;
    }

    private sealed class Presence
    {
        public long? MapId { get; set; }

        public bool Counted { get; set; } = true;
    }
}
