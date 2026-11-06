namespace D3B.App.ViewModels;

/// <summary>A behavior of the project a new profile may play: a list of steps, or what the canvas of 7e will make (D386).</summary>
/// <param name="Id">The behavior's id, or <see langword="null"/> for a new list of steps, made with the profile.</param>
/// <param name="Name">Its name.</param>
/// <param name="Detail">What it is made of, in words.</param>
internal sealed record BehaviorChoiceViewModel(string? Id, string Name, string Detail)
{
    /// <summary>Gets whether the choice makes a new list of steps.</summary>
    public bool IsNew => Id is null;
}
