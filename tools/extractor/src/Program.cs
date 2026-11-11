using System.Globalization;
using D3B.Extractor;

// Extracts the game data of the installed Dofus 3 client into one JSON folder per build (docs/data.md). The folder is
// derived from Ankama's client, so it is never committed: it ships as the client-<build> release asset (ADR 0008).
// Exit code: 0 on success, 3 when the command line is wrong.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return ExtractCommand.Run(args, Console.Out, Console.Error);
