using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Items;

/// <summary>The jobs of the data folder and their skills: <c>jobs.json</c> and <c>skills.json</c>.</summary>
/// <remarks>
/// A job harvests when a skill of it gathers an item; which ones are professions will be derived in Rust by step 3's
/// PR 4 (3.7.13.17, <c>artifacts/studies/items.md</c>: jobs 2, 24, 26, 28 and 36, whose resources the sub-areas list).
/// </remarks>
internal static class JobTables
{
    /// <summary>Reads the jobs and their skills from the client and writes them into the folder, with the skills gathering one of <paramref name="items"/> or none.</summary>
    /// <exception cref="InvalidDataException">A job or a skill names text 0, an id is held twice, or a skill is of a job the client lacks or gathers a negative item other than -1.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, IReadOnlyList<Item> items, DataFolder folder, TextWriter output)
    {
        var jobs = ReadJobs(DataTable.ReadById(client, "jobs"), texts);
        var (skills, skipped) = Kept(ReadSkills(DataTable.ReadById(client, "skills"), texts), jobs, items);

        folder.Write("jobs.json", WriteJobs(jobs));
        folder.Write("skills.json", WriteSkills(skills));

        output.WriteLine($"Jobs: {jobs.Count:N0} jobs, {skills.Count:N0} skills; left out {skipped:N0} gathering an item items.json lacks");
    }

    /// <exception cref="InvalidDataException">A job names text 0.</exception>
    public static IReadOnlyList<Job> ReadJobs(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new Job { Id = id, Name = ItemTables.Named("Job", id, texts.Name(record.Int32("nameId"))), IconId = record.Int32("iconId") };
                })
                .OrderBy(job => job.Id),
        ];
    }

    /// <exception cref="InvalidDataException">A skill names text 0, or gathers a negative item other than -1.</exception>
    public static IReadOnlyList<Skill> ReadSkills(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new Skill
                    {
                        Id = id,
                        Name = ItemTables.Named("Skill", id, texts.Name(record.Int32("nameId"))),
                        ParentJobId = record.Int32("parentJobId"),
                        GatheredItemId = GatheredOf(id, record.Int32("gatheredRessourceItem")),
                        LevelMin = record.Int32("levelMin"),
                        Range = record.Int32("range"),
                        ClientDisplay = record.Boolean("clientDisplay"),
                    };
                })
                .OrderBy(skill => skill.Id),
        ];
    }

    /// <summary>Reads the item a skill gathers, -1 or 0 for none (240 and 8 skills on 3.7.13.17).</summary>
    /// <exception cref="InvalidDataException">The item is negative but not -1, which is not measured.</exception>
    public static int? GatheredOf(int skill, int item)
    {
        return item switch
        {
            > 0 => item,
            0 or -1 => null,
            _ => throw new InvalidDataException($"Skill {skill} gathers item {item}."),
        };
    }

    /// <summary>Leaves out the skills gathering an item the items lack, and counts them.</summary>
    /// <remarks>Skill 260 gathers item 3,656, which 3.7.13.17 lacks: left out by the owner's choice of 2026-11-14.</remarks>
    /// <exception cref="InvalidDataException">A job or a skill id is held twice, or a skill is of a job the client lacks.</exception>
    public static (IReadOnlyList<Skill> Skills, int Skipped) Kept(IReadOnlyList<Skill> skills, IReadOnlyList<Job> jobs, IReadOnlyList<Item> items)
    {
        var known = ItemTables.Unique("jobs", jobs, job => job.Id);

        ItemTables.Unique("skills", skills, skill => skill.Id);

        if (skills.FirstOrDefault(skill => !known.Contains(skill.ParentJobId)) is { } orphan)
        {
            throw new InvalidDataException($"Skill {orphan.Id} is of job {orphan.ParentJobId}, which the client lacks.");
        }

        var gathered = items.Select(item => item.Id).ToHashSet();
        IReadOnlyList<Skill> kept = [.. skills.Where(skill => skill.GatheredItemId is not { } item || gathered.Contains(item))];

        return (kept, skills.Count - kept.Count);
    }

    public static byte[] WriteJobs(IEnumerable<Job> jobs)
    {
        return JsonTable.Write(jobs, (json, job) =>
        {
            json.WriteNumber("id", job.Id);
            JsonTable.WriteText(json, "name", job.Name);
            json.WriteNumber("iconId", job.IconId);
        });
    }

    public static byte[] WriteSkills(IEnumerable<Skill> skills)
    {
        return JsonTable.Write(skills, (json, skill) =>
        {
            json.WriteNumber("id", skill.Id);
            JsonTable.WriteText(json, "name", skill.Name);
            json.WriteNumber("parentJobId", skill.ParentJobId);

            if (skill.GatheredItemId is { } item)
            {
                json.WriteNumber("gatheredItemId", item);
            }
            else
            {
                json.WriteNull("gatheredItemId");
            }

            json.WriteNumber("levelMin", skill.LevelMin);
            json.WriteNumber("range", skill.Range);
            json.WriteBoolean("clientDisplay", skill.ClientDisplay);
        });
    }
}
