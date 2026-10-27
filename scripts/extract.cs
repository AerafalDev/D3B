#!/usr/bin/env dotnet
#:package AssetsTools.NET
#:package System.CommandLine
#:project ../src/D3B.Data/D3B.Data.csproj
#:include extract/ClientMapLayout.cs
#:include extract/ExtractCommand.cs
#:include extract/InputFingerprint.cs
#:include extract/MapReader.cs
#:include extract/MapsExtractor.cs
#:include extract/TextsExtractor.cs
#:include extract/WorldExtractor.cs
#:include extract/WorldGraphReader.cs
#:include unity/ClientInstall.cs
#:include unity/I18nFile.cs
#:include unity/LauncherManifest.cs
#:include unity/UnityBundle.cs
#:include unity/UnityDataTable.cs
#:include unity/UnityFields.cs
#:include unity/UnityReader.cs
#:include unity/UnityReferences.cs
#:include unity/UnityReferenceVisitor.cs
#:include unity/UnityTypeNode.cs
#:include unity/UnityTypeTree.cs

using System.Globalization;
using D3B.Scripts.Extract;

// Extracts the game data of the installed client into the pack of its build (D10, D24): the numbered texts in French
// and English, every map with its cells and interactive elements, the world graph, and the atlas of map positions,
// sub-areas, areas and zaaps.
// Usage: dotnet run --file scripts/extract.cs -- [--client <dir>] [--output <dir>] [--force] [--level <n>]; --help
// describes the options.
// The pack lands in <output>/<client version>/data-<format>.pack, where the host looks first. It is derived from the
// client, so it is never committed: packs ship as release artifacts (D24).
// Exit code: 0 on success, or when the pack is already up to date; 1 when the extraction failed; 2 when the client is
// missing.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return ExtractCommand.Create().Parse(args).Invoke();
