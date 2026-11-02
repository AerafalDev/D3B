using Avalonia;
using Avalonia.Controls.Primitives;

namespace D3B.App.Controls;

/// <summary>A tooltip's content: what a control does, and its shortcut as keycaps on the right (D320, D322).</summary>
internal sealed class KeyTip : TemplatedControl
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<KeyTip, string?>(nameof(Text));

    /// <summary>Defines the <see cref="Keys"/> property.</summary>
    public static readonly StyledProperty<string?> KeysProperty = AvaloniaProperty.Register<KeyTip, string?>(nameof(Keys));

    /// <summary>Defines the <see cref="KeyCaps"/> property.</summary>
    public static readonly DirectProperty<KeyTip, IReadOnlyList<string>> KeyCapsProperty =
        AvaloniaProperty.RegisterDirect<KeyTip, IReadOnlyList<string>>(nameof(KeyCaps), tip => tip.KeyCaps);

    private IReadOnlyList<string> _keyCaps = [];

    /// <summary>Gets or sets what the control does.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the shortcut, its keys apart by spaces, such as <c>Ctrl K</c>; none by default.</summary>
    public string? Keys
    {
        get => GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    /// <summary>Gets the shortcut's keys, one keycap each.</summary>
    public IReadOnlyList<string> KeyCaps
    {
        get => _keyCaps;
        private set => SetAndRaise(KeyCapsProperty, ref _keyCaps, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        if (change.Property == KeysProperty)
            KeyCaps = Keys?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    }
}
