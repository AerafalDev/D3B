namespace D3B.Bot.Npcs;

/// <summary>An NPC the bots saw on a map: which, its actor id, where it stood and which way it faced, and how often it was seen there (D383).</summary>
/// <param name="MapId">The map.</param>
/// <param name="NpcId">Which NPC it is, by id in the client's tables.</param>
/// <param name="ActorId">Its actor id on the map, negative, as the last map information gave it.</param>
/// <param name="Cell">The cell it stood on when last seen, when the server gave one.</param>
/// <param name="Direction">The way it faced when last seen, 0 to 7 numbered as the directions of D27, when the server gave one.</param>
/// <param name="Seen">How many times a map's information listed it.</param>
/// <param name="FirstSeen">When it first did.</param>
/// <param name="LastSeen">When it last did.</param>
public sealed record KnownMapNpc(long MapId, int NpcId, int ActorId, int? Cell, int? Direction, int Seen, DateTimeOffset FirstSeen, DateTimeOffset LastSeen);
