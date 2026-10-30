#!/usr/bin/env dotnet
#:package Google.Protobuf
#:package System.CommandLine
#:project ../src/D3B.Bot/D3B.Bot.csproj
#:include fights/CapturedFights.cs
#:include fights/CastBench.cs
#:include fights/FightsCommand.cs
#:include fights/Tally.cs
#:include fights/TurnBench.cs

using System.Globalization;
using D3B.Scripts.Fights;

// The bench of the effects' engine (D177, D195): replays the fights of the game captures through the bot's fight state,
// and compares, action by action, what D3B.Engine foresees of each cast a fighter chose with what the server announced:
// damage by element, steals, heals, shields, pushes and their damage, AP and MP removed, teleports, swaps, summons,
// states and buffs, the rules of each cast, and the spells' cooldowns. It prints counts, never a value of the fights: a
// capture holds an account's login ticket, and what this tool prints may go into docs/client.md.
// With --turns, it replays every turn the characters played through the fight's AI instead, its plan against the turn
// played and what its judge foresaw against what came (D204).
// Usage: dotnet run --file scripts/fights.cs -- [<file or directory>...] [--data <dir>] [--details] [--turns]; --help describes
// the options.
// Exit code: 0 once every capture was read; 1 when one could not be; 2 when the pack, the schema, the mapping or the
// captures are missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return FightsCommand.Create().Parse(args).Invoke();
