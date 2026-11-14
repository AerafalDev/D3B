using D3B.Extractor.Items;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.World;

namespace D3B.Extractor.Tests.Items;

public sealed class JobTablesTests
{
    private static readonly Job _base = new() { Id = 1, Name = new Text { Fr = "Base", En = "Base", De = "Basis", Es = "Base", Pt = "Base" }, IconId = -1 };
    private static readonly Job _lumberjack = new() { Id = 2, Name = new Text { Fr = "Bûcheron", En = "Lumberjack", De = "Holzfäller", Es = "Leñador", Pt = "Lenhador" }, IconId = 1 };
    private static readonly Skill _cut = new()
    {
        Id = 6,
        Name = new Text { Fr = "Couper", En = "Cut", De = "Fällen", Es = "Talar", Pt = "Cortar" },
        ParentJobId = 2,
        GatheredItemId = 303,
        LevelMin = 1,
        Range = 1,
        ClientDisplay = true,
    };

    private static readonly Skill _nameless = new() { Id = 299, Name = null, ParentJobId = 1, GatheredItemId = null, LevelMin = 1, Range = 10, ClientDisplay = false };
    private static readonly Item _wood = new() { Id = 303, Name = _cut.Name!, TypeId = 38, Level = 1, IconId = 0 };

    [Fact]
    public void JobsAreWrittenAsTheAppReadsThem()
    {
        // The app's own tests read the same fixtures (src-tauri/src/data/items/).
        Assert.Equal(Fixture("jobs.json"), JobTables.WriteJobs([_base, _lumberjack]));
    }

    [Fact]
    public void SkillsAreWrittenAsTheAppReadsThem()
    {
        Assert.Equal(Fixture("skills.json"), JobTables.WriteSkills([_cut, _nameless]));
    }

    [Theory]
    [InlineData(303, 303)]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    public void ASkillGathersItsItemOrNone(int item, int? gathered)
    {
        Assert.Equal(gathered, JobTables.GatheredOf(6, item));
    }

    [Fact]
    public void ASkillGatheringAnotherNegativeItemFails()
    {
        var failure = Assert.Throws<InvalidDataException>(() => JobTables.GatheredOf(6, -2));

        Assert.Equal("Skill 6 gathers item -2.", failure.Message);
    }

    [Fact]
    public void SkillsGatheringAnItemOrNoneAreKept()
    {
        var (skills, skipped) = JobTables.Kept([_cut, _nameless], [_base, _lumberjack], [_wood]);

        Assert.Equal([6, 299], skills.Select(skill => skill.Id));
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ASkillGatheringAnItemTheItemsLackIsLeftOutAndCounted()
    {
        var (skills, skipped) = JobTables.Kept([_cut, _nameless], [_base, _lumberjack], []);

        Assert.Equal([299], skills.Select(skill => skill.Id));
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void ASkillOfAJobTheClientLacksFails()
    {
        var failure = Assert.Throws<InvalidDataException>(() => JobTables.Kept([_cut, _nameless], [_base], [_wood]));

        Assert.Equal("Skill 6 is of job 2, which the client lacks.", failure.Message);
    }

    [Fact]
    public void TheInstalledClientsJobsAndSkillsAreKeptButTheOneGatheringAMissingItem()
    {
        var client = Installed.Client("3.7.13.17");
        var texts = TextFiles.Open(client);
        var types = ItemTables.ReadTypes(DataTable.ReadById(client, "itemtypes"), texts);
        var (items, _) = ItemTables.Kept(ItemTables.ReadItems(DataTable.ReadById(client, "items"), texts), types);
        var jobs = JobTables.ReadJobs(DataTable.ReadById(client, "jobs"), texts);
        var (skills, skipped) = JobTables.Kept(JobTables.ReadSkills(DataTable.ReadById(client, "skills"), texts), jobs, items);

        // 3.7.13.17, the study of the items of 2026-11-14: 373 skills, 260 gathering item 3,656, which the client lacks.
        Assert.Equal((23, 372, 1), (jobs.Count, skills.Count, skipped));
        Assert.DoesNotContain(skills, skill => skill.Id == 260);

        // The professions are the jobs whose skills gather what a sub-area lists: Base gathers too, but none of those.
        var listed = WorldTables.ReadSubAreas(DataTable.ReadById(client, "subareas"), texts).SelectMany(subArea => subArea.Harvestables).ToHashSet();
        var professions = skills.Where(skill => skill.GatheredItemId is { } item && listed.Contains(item)).Select(skill => skill.ParentJobId).Distinct().Order();

        Assert.Equal([2, 24, 26, 28, 36], professions);
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "items", name));
    }
}
