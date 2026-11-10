namespace D3B.App.ViewModels.Routes;

/// <summary>Makes the menu of the worlds the layer can show (D299).</summary>
internal static class RouteWorldChoices
{
    /// <summary>Makes a choice for each world that holds maps.</summary>
    /// <param name="page">The routes, which hold the localizer and what the editors read.</param>
    /// <param name="texts">Names the worlds.</param>
    /// <param name="layer">The world shown.</param>
    /// <param name="use">What choosing a world does, given its id.</param>
    /// <returns>The choices, in the catalog's order.</returns>
    public static List<WorldChoiceViewModel> Build(RoutesViewModel page, RouteTexts texts, int layer, Action<int> use)
    {
        var services = page.Services!;
        var worlds = new List<WorldChoiceViewModel>();

        foreach (var world in services.Catalog.Worlds.Span)
        {
            var count = services.Index.SquaresOf(world.Id).ToArray().Sum(square => square.Count);

            if (count == 0)
                continue;

            worlds.Add(new WorldChoiceViewModel(world.Id, texts.WorldName(world), count, page.Localizer.Plural("RouteWorldMaps", count), world.Id == layer, use));
        }

        return worlds;
    }
}
