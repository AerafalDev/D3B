namespace D3B.App.ViewModels.Routes;

/// <summary>A world the layer can show, in the menu of the map's top left (D299).</summary>
/// <param name="Id">The world's id.</param>
/// <param name="Name">Its name in the game's language.</param>
/// <param name="Count">How many maps it holds, its insides included.</param>
/// <param name="Detail">The count, said.</param>
/// <param name="IsChosen">Whether it is the layer shown.</param>
internal sealed record WorldChoiceViewModel(int Id, string Name, int Count, string Detail, bool IsChosen);
