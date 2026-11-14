using System.Globalization;

namespace D3B.Extractor.Items;

/// <summary>The items' and jobs' icons of the data folder: <c>icons/items/&lt;iconId&gt;.png</c> and <c>icons/jobs/&lt;iconId&gt;.png</c>, 32 px.</summary>
/// <remarks>
/// Only the icons a written item or job names are written: 11,768 of the 12,295 item textures on 3.7.13.17. Four items
/// name an icon the client lacks (102,240, 102,280, 102,400 and 102,440): they are counted and keep their
/// <c>iconId</c>, and the app shows them without one. A job's icon is never missing; -1 is none (job 1, Base).
/// </remarks>
internal static class IconTables
{
    /// <summary>The <c>iconId</c> of a job without an icon.</summary>
    public const int NoIcon = -1;

    /// <summary>Reads the icons of <paramref name="items"/> and <paramref name="jobs"/> from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">A bundle holds a texture the extractor does not read, or a job's icon is missing.</exception>
    public static void Extract(ClientInstall client, IReadOnlyList<Item> items, IReadOnlyList<Job> jobs, DataFolder folder, TextWriter output)
    {
        var (itemIcons, missing) = OfItems(items, ReadItemIcons(client));
        var jobIcons = OfJobs(jobs, ReadJobIcons(client));

        foreach (var (icon, png) in itemIcons)
        {
            folder.Write(PathOf("items", icon), png);
        }

        foreach (var (icon, png) in jobIcons)
        {
            folder.Write(PathOf("jobs", icon), png);
        }

        var bytes = itemIcons.Values.Concat(jobIcons.Values).Sum(png => (long)png.Length);

        output.WriteLine($"Icons: {itemIcons.Count:N0} items, {jobIcons.Count:N0} jobs at {IconPictures.Size} px, {bytes:N0} bytes; {missing:N0} items name an icon the client lacks");
    }

    /// <summary>Reads every item icon of the client: BC7 or RGBA32, from 32 to 64 px.</summary>
    /// <exception cref="InvalidDataException">A texture is of another shape or format, or not named by a number, or named twice.</exception>
    public static SortedDictionary<int, byte[]> ReadItemIcons(ClientInstall client)
    {
        return IconPictures.Read(client, IconPictures.Largest, [IconPictures.Bc7Format, IconPictures.Rgba32Format], "Content", "Picto", "Items", "item_assets_1x.bundle");
    }

    /// <summary>Reads every job icon of the client: BC7 at 32 px, kept as it is.</summary>
    /// <exception cref="InvalidDataException">A texture is of another shape or format, or not named by a number, or named twice.</exception>
    public static SortedDictionary<int, byte[]> ReadJobIcons(ClientInstall client)
    {
        return IconPictures.Read(client, IconPictures.Size, [IconPictures.Bc7Format], "Content", "Picto", "UI", "job_assets_1x.bundle");
    }

    /// <summary>Keeps the icons the items name, and counts the items naming one the client lacks.</summary>
    public static (SortedDictionary<int, byte[]> Icons, int Missing) OfItems(IReadOnlyList<Item> items, IReadOnlyDictionary<int, byte[]> icons)
    {
        var kept = new SortedDictionary<int, byte[]>();
        var missing = 0;

        foreach (var item in items)
        {
            if (icons.TryGetValue(item.IconId, out var png))
            {
                kept.TryAdd(item.IconId, png);
            }
            else
            {
                missing++;
            }
        }

        return (kept, missing);
    }

    /// <summary>Keeps the icons the jobs name.</summary>
    /// <exception cref="InvalidDataException">A job names an icon the client lacks.</exception>
    public static SortedDictionary<int, byte[]> OfJobs(IReadOnlyList<Job> jobs, IReadOnlyDictionary<int, byte[]> icons)
    {
        var kept = new SortedDictionary<int, byte[]>();

        foreach (var job in jobs.Where(job => job.IconId != NoIcon))
        {
            kept.TryAdd(job.IconId, icons.TryGetValue(job.IconId, out var png) ? png : throw new InvalidDataException($"Job {job.Id} names icon {job.IconId}, which the client lacks."));
        }

        return kept;
    }

    /// <summary>The path of an icon in the data folder: <paramref name="kind"/> is <c>items</c> or <c>jobs</c>.</summary>
    public static string PathOf(string kind, int icon)
    {
        return string.Create(CultureInfo.InvariantCulture, $"icons/{kind}/{icon}.png");
    }
}
