using Avalonia.Media;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>A kind the Add menu offers, disabled with its session until D3B makes it (D326).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Name">Its name, in the app's language.</param>
/// <param name="Session">The session that makes its editor, or <see langword="null"/> for one made today.</param>
internal sealed record AssetKindChoice(AssetKind Kind, string Name, string? Session)
{
    /// <summary>Gets whether D3B makes the kind today.</summary>
    public bool IsEnabled => Session is null;

    /// <summary>Gets the kind's IntelliJ icon, by its key.</summary>
    public string Icon => AssetKinds.IconOf(Kind);

    /// <summary>Gets the kind's colour.</summary>
    public IBrush KindBrush => new SolidColorBrush(AssetKinds.ColorOf(Kind));

    /// <summary>Gets the kind's dark tint.</summary>
    public IBrush KindTint => new SolidColorBrush(AssetKinds.TintOf(Kind));
}
