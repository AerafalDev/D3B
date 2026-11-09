using System.Collections.Immutable;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>Reads a route's steps as clips and edits them through clips, leaving every file's detail as it was (D395, D396).</summary>
/// <remarks>
/// <para>
/// A clip is the longest run of consecutive steps that hold an equal action, so that the route's file, which keeps each step's
/// actions in order (D378), needs no other form. Reading loses nothing: every action of every step is in exactly one clip.
/// </para>
/// <para>
/// Editing changes the steps by the least it can. An action added goes at the end of its step's actions, one taken off leaves
/// the others where they were, and a step that already holds the action is left alone, so that drawing over an action never
/// doubles it. The step's map, its pin and what the file holds that the version does not read are never touched, and a step
/// the edit does not reach is the very same object.
/// </para>
/// <para>
/// Two clips of equal actions that come to touch read as one clip: the file cannot tell them apart.
/// </para>
/// </remarks>
public static class RouteClips
{
    /// <summary>Reads a route's steps as clips.</summary>
    /// <param name="anchors">The route's steps.</param>
    /// <returns>
    /// The clips, grouped by kind in the order of the editor's menu, an action the host does not know last, then by first step
    /// and layer.
    /// </returns>
    public static ImmutableArray<RouteClip> Derive(IReadOnlyList<RouteAnchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var clips = new List<RouteClip>();
        var open = new List<RouteClip>();

        for (var step = 0; step < anchors.Count; step++)
        {
            var present = Present(anchors[step]);
            var carried = new List<RouteClip>();

            foreach (var clip in open)
            {
                var at = present.FindIndex(candidate => candidate.Layer == clip.Layer && RouteAnchor.SameCall(candidate.Call, clip.Call));

                if (at < 0)
                {
                    clips.Add(clip);

                    continue;
                }

                present.RemoveAt(at);
                carried.Add(clip with { Last = step });
            }

            foreach (var (call, layer) in present)
                carried.Add(new RouteClip(call, step, step, layer, 0));

            open = carried;
        }

        clips.AddRange(open);

        return Lanes(clips);
    }

    /// <summary>Puts an action on every step of a range that does not hold it already.</summary>
    /// <param name="anchors">The route's steps.</param>
    /// <param name="call">The action.</param>
    /// <param name="first">The first step of the range, from 0.</param>
    /// <param name="last">The last step of the range.</param>
    /// <param name="layer">Which of the equal actions of a step it is, from 0: a copy of the second of two puts the second.</param>
    /// <returns>The route's steps after the edit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is backwards or leaves the route.</exception>
    public static ImmutableArray<RouteAnchor> Draw(IReadOnlyList<RouteAnchor> anchors, NodeCall call, int first, int last, int layer = 0)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(call);

        return Reshape(anchors, new RouteClip(call, first, first - 1, layer, 0), first, last);
    }

    /// <summary>Moves a clip's edges: puts its action on the steps the range gains and takes it off those it leaves.</summary>
    /// <remarks>A clip moved is a clip reshaped with the same length.</remarks>
    /// <param name="anchors">The route's steps.</param>
    /// <param name="clip">The clip, as <see cref="Derive"/> gave it.</param>
    /// <param name="first">The new first step, from 0.</param>
    /// <param name="last">The new last step.</param>
    /// <returns>The route's steps after the edit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is backwards or leaves the route.</exception>
    public static ImmutableArray<RouteAnchor> Reshape(IReadOnlyList<RouteAnchor> anchors, RouteClip clip, int first, int last)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(clip);

        if (first < 0 || last < first || last >= anchors.Count)
            throw new ArgumentOutOfRangeException(nameof(last), $"The range {first} to {last} is not inside a route of {anchors.Count} steps.");

        var result = anchors.ToImmutableArray().ToBuilder();

        for (var step = Math.Min(first, clip.First); step <= Math.Max(last, clip.Last); step++)
        {
            var wanted = step >= first && step <= last;

            if (clip.Covers(step) && !wanted)
                result[step] = Without(result[step], clip);
            else if (wanted && !clip.Covers(step))
                result[step] = With(result[step], clip);
        }

        return result.ToImmutable();
    }

    /// <summary>Takes a clip's action off every step it covers.</summary>
    /// <param name="anchors">The route's steps.</param>
    /// <param name="clip">The clip, as <see cref="Derive"/> gave it.</param>
    /// <returns>The route's steps after the edit.</returns>
    public static ImmutableArray<RouteAnchor> Remove(IReadOnlyList<RouteAnchor> anchors, RouteClip clip)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(clip);
        CheckInside(anchors, clip);

        var result = anchors.ToImmutableArray().ToBuilder();

        for (var step = clip.First; step <= clip.Last; step++)
            result[step] = Without(result[step], clip);

        return result.ToImmutable();
    }

    /// <summary>Gives a clip's action other values, where it stands on every step it covers.</summary>
    /// <param name="anchors">The route's steps.</param>
    /// <param name="clip">The clip, as <see cref="Derive"/> gave it.</param>
    /// <param name="call">The action with its new values.</param>
    /// <returns>The route's steps after the edit.</returns>
    public static ImmutableArray<RouteAnchor> Revalue(IReadOnlyList<RouteAnchor> anchors, RouteClip clip, NodeCall call)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(call);
        CheckInside(anchors, clip);

        var result = anchors.ToImmutableArray().ToBuilder();

        for (var step = clip.First; step <= clip.Last; step++)
        {
            var at = IndexOf(result[step], clip.Call, clip.Layer);

            if (at >= 0)
                result[step] = result[step] with { Actions = result[step].Actions.SetItem(at, call) };
        }

        return result.ToImmutable();
    }

    private static void CheckInside(IReadOnlyList<RouteAnchor> anchors, RouteClip clip)
    {
        if (clip.First < 0 || clip.Last < clip.First || clip.Last >= anchors.Count)
            throw new ArgumentOutOfRangeException(nameof(clip), $"The clip's steps {clip.First} to {clip.Last} are not inside a route of {anchors.Count} steps.");
    }

    private static List<(NodeCall Call, int Layer)> Present(RouteAnchor anchor)
    {
        var present = new List<(NodeCall, int)>(anchor.Actions.Length);

        foreach (var call in anchor.Actions)
        {
            var layer = present.Count(earlier => RouteAnchor.SameCall(earlier.Item1, call));

            present.Add((call, layer));
        }

        return present;
    }

    private static int IndexOf(RouteAnchor anchor, NodeCall call, int layer)
    {
        var seen = 0;

        for (var i = 0; i < anchor.Actions.Length; i++)
        {
            if (!RouteAnchor.SameCall(anchor.Actions[i], call))
                continue;

            if (seen == layer)
                return i;

            seen++;
        }

        return -1;
    }

    private static int Count(RouteAnchor anchor, NodeCall call)
    {
        return anchor.Actions.Count(held => RouteAnchor.SameCall(held, call));
    }

    private static RouteAnchor With(RouteAnchor anchor, RouteClip clip)
    {
        return Count(anchor, clip.Call) > clip.Layer ? anchor : anchor with { Actions = anchor.Actions.Add(clip.Call) };
    }

    private static RouteAnchor Without(RouteAnchor anchor, RouteClip clip)
    {
        var at = IndexOf(anchor, clip.Call, clip.Layer);

        return at < 0 ? anchor : anchor with { Actions = anchor.Actions.RemoveAt(at) };
    }

    private static ImmutableArray<RouteClip> Lanes(List<RouteClip> clips)
    {
        var ordered = clips
            .OrderBy(clip => Rank(clip.Call.Node))
            .ThenBy(clip => clip.Call.Node, StringComparer.Ordinal)
            .ThenBy(clip => clip.First)
            .ThenBy(clip => clip.Layer)
            .ToList();
        var result = ImmutableArray.CreateBuilder<RouteClip>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var clip = ordered[i];
            var lane = 0;

            while (result.Any(placed => placed.Call.Node == clip.Call.Node && placed.Lane == lane && placed.Last >= clip.First))
                lane++;

            result.Add(clip with { Lane = lane });
        }

        return result.ToImmutable();
    }

    private static int Rank(string node)
    {
        var at = RouteActions.Offered.IndexOf(node);

        return at < 0 ? RouteActions.Offered.Length : at;
    }
}
