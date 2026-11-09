using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using D3B.App.Controls.WorldMaps;

namespace D3B.App.Controls.Sequencer;

/// <summary>The Sequencer's grid: a ruler of the route's steps, a row of their zones and a track for each kind of action, a clip drawn across the steps it covers (D395, D396).</summary>
/// <remarks>
/// It draws what it is given and tells what was pressed; it knows nothing of routes. The names of the tracks stand beside it, in
/// rows of the heights <see cref="SequencerLayout"/> gives both.
/// </remarks>
internal sealed class SequencerGrid : Control
{
    /// <summary>Identifies <see cref="Scene"/>.</summary>
    public static readonly StyledProperty<SequencerScene> SceneProperty = AvaloniaProperty.Register<SequencerGrid, SequencerScene>(nameof(Scene), SequencerScene.Empty);

    /// <summary>Identifies <see cref="SelectedSteps"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> SelectedStepsProperty = AvaloniaProperty.Register<SequencerGrid, IReadOnlyList<int>?>(nameof(SelectedSteps));

    /// <summary>Identifies <see cref="SelectedClip"/>.</summary>
    public static readonly StyledProperty<int> SelectedClipProperty = AvaloniaProperty.Register<SequencerGrid, int>(nameof(SelectedClip), -1);

    private static readonly IBrush _band = new SolidColorBrush(Color.Parse("#3B82F6"), 0.16);
    private static readonly IBrush _bandRuler = new SolidColorBrush(Color.Parse("#3B82F6"), 0.28);
    private static readonly IBrush _zone = new SolidColorBrush(Colors.White, 0.05);
    private static readonly IBrush _bright = new SolidColorBrush(Color.Parse("#F4F4F6"));
    private static readonly IBrush _muted = new SolidColorBrush(Color.Parse("#8E9099"));
    private static readonly IBrush _clipText = new SolidColorBrush(Color.Parse("#0A0E0B"));
    private static readonly IPen _line = new Pen(new SolidColorBrush(Colors.White, 0.07), 1);
    private static readonly IPen _faint = new Pen(new SolidColorBrush(Colors.White, 0.035), 1);
    private static readonly IPen _selected = new Pen(new SolidColorBrush(Color.Parse("#3B82F6")), 2);
    private static readonly IPen _problem = new Pen(new SolidColorBrush(Color.Parse("#EA5D52")), 1.5);

    /// <summary>Gets or sets what is drawn.</summary>
    public SequencerScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets the steps selected, whose columns are tinted.</summary>
    public IReadOnlyList<int>? SelectedSteps
    {
        get => GetValue(SelectedStepsProperty);
        set => SetValue(SelectedStepsProperty, value);
    }

    /// <summary>Gets or sets the place in the scene of the clip selected, or -1.</summary>
    public int SelectedClip
    {
        get => GetValue(SelectedClipProperty);
        set => SetValue(SelectedClipProperty, value);
    }

    /// <summary>Gets the width of a step's column now.</summary>
    public double ColumnWidth
    {
        get
        {
            var steps = Scene.Steps.Count;

            return steps == 0 ? 0 : Math.Max(SequencerLayout.MinColumn, Bounds.Width / steps);
        }
    }

    /// <summary>Raised when a step is pressed, on the ruler, the zones' row or an empty cell: its place, from 0, and the keys held.</summary>
    public event Action<int, KeyModifiers>? StepPressed;

    /// <summary>Raised when a clip is pressed: its place in the scene.</summary>
    public event Action<int>? ClipPressed;

    /// <summary>Initializes static members of the <see cref="SequencerGrid"/> class.</summary>
    static SequencerGrid()
    {
        AffectsMeasure<SequencerGrid>(SceneProperty);
        AffectsRender<SequencerGrid>(SceneProperty, SelectedStepsProperty, SelectedClipProperty);
        FocusableProperty.OverrideDefaultValue<SequencerGrid>(true);
    }

    private static void Write(DrawingContext context, string text, Typeface typeface, double size, IBrush brush, double x, double y, double width, bool centered)
    {
        if (text.Length == 0 || width <= 4)
            return;

        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush)
        {
            MaxTextWidth = width,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        context.DrawText(formatted, new Point(centered ? x - (Math.Min(formatted.Width, width) / 2) : x, y));
    }

    /// <summary>Gets the rectangle of a clip, or <see langword="null"/> when the scene has none by that place.</summary>
    /// <param name="id">The clip's place in the scene.</param>
    /// <returns>The rectangle, in the grid's own pixels.</returns>
    public Rect? RectOfClip(int id)
    {
        var y = SequencerLayout.RulerHeight + SequencerLayout.MovementHeight;

        foreach (var track in Scene.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.Id == id)
                    return ClipRect(clip, y);
            }

            y += track.Height;
        }

        return null;
    }

    /// <summary>Gets the rectangle of a step's column on the ruler.</summary>
    /// <param name="step">The step, from 0.</param>
    /// <returns>The rectangle.</returns>
    public Rect RectOfStep(int step)
    {
        return new Rect(step * ColumnWidth, 0, ColumnWidth, SequencerLayout.RulerHeight);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(Scene.Steps.Count * SequencerLayout.MinColumn, SequencerLayout.RulerHeight + Scene.BodyHeight);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // A control that draws nothing under the pointer is not hit: this one takes every press of its rectangle.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

        var scene = Scene;
        var width = ColumnWidth;

        if (scene.Steps.Count == 0)
            return;

        var height = SequencerLayout.RulerHeight + scene.BodyHeight;
        var chosen = SelectedSteps ?? [];
        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.Medium);
        var bold = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.SemiBold);

        foreach (var step in chosen)
        {
            context.DrawRectangle(_band, null, new Rect(step * width, SequencerLayout.RulerHeight, width, height - SequencerLayout.RulerHeight));
            context.DrawRectangle(_bandRuler, null, new Rect(step * width, 0, width, SequencerLayout.RulerHeight));
        }

        for (var i = 0; i < scene.Steps.Count; i++)
        {
            var cell = new Rect(i * width, 0, width, SequencerLayout.RulerHeight);
            var step = scene.Steps[i];

            Write(context, step.Number, bold, 12, _bright, cell.Center.X, 6, width - 4, centered: true);
            Write(context, step.Place, typeface, 10.5, _muted, cell.Center.X, 24, width - 4, centered: true);

            if (i > 0)
                context.DrawLine(_faint, new Point(i * width, SequencerLayout.RulerHeight), new Point(i * width, height));
        }

        context.DrawLine(_line, new Point(0, SequencerLayout.RulerHeight), new Point(Bounds.Width, SequencerLayout.RulerHeight));

        for (var i = 0; i < scene.Steps.Count; i++)
        {
            var zone = new Rect((i * width) + 3, SequencerLayout.RulerHeight + 3, width - 6, SequencerLayout.MovementHeight - 6);

            context.DrawRectangle(_zone, null, new RoundedRect(zone, 6));

            using (context.PushClip(zone))
                Write(context, scene.Steps[i].Zone, typeface, 10.5, _muted, zone.X + 6, zone.Y + 3, zone.Width - 10, centered: false);
        }

        var y = SequencerLayout.RulerHeight + SequencerLayout.MovementHeight;

        foreach (var track in scene.Tracks)
        {
            context.DrawLine(_line, new Point(0, y), new Point(Bounds.Width, y));

            foreach (var clip in track.Clips)
                DrawClip(context, track, clip, y, typeface);

            y += track.Height;
        }

        context.DrawLine(_line, new Point(0, y), new Point(Bounds.Width, y));
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus();

        var at = e.GetPosition(this);

        if (ClipAt(at) is { } clip)
            ClipPressed?.Invoke(clip.Id);
        else if (StepAt(at) is { } step)
            StepPressed?.Invoke(step, e.KeyModifiers);
        else
            return;

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Cursor = ClipAt(e.GetPosition(this)) is null ? null : new Cursor(StandardCursorType.Hand);
    }

    private Rect ClipRect(SequencerClip clip, double trackTop)
    {
        var width = ColumnWidth;

        return new Rect((clip.First * width) + 2, trackTop + (clip.Lane * SequencerLayout.LaneHeight) + 3, ((clip.Last - clip.First + 1) * width) - 4, SequencerLayout.LaneHeight - 6);
    }

    private void DrawClip(DrawingContext context, SequencerTrack track, SequencerClip clip, double trackTop, Typeface typeface)
    {
        var rect = ClipRect(clip, trackTop);
        var fill = new SolidColorBrush(WorldMapBackdropLayer.ColorOf(track.Kind));
        var pen = clip.Id == SelectedClip ? _selected : clip.HasProblems ? _problem : null;

        context.DrawRectangle(fill, pen, new RoundedRect(rect, 7));

        using (context.PushClip(rect))
            Write(context, clip.Text, typeface, 11.5, _clipText, rect.X + 8, rect.Y + 4, rect.Width - 14, centered: false);
    }

    private SequencerClip? ClipAt(Point at)
    {
        var y = SequencerLayout.RulerHeight + SequencerLayout.MovementHeight;

        foreach (var track in Scene.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (ClipRect(clip, y).Contains(at))
                    return clip;
            }

            y += track.Height;
        }

        return null;
    }

    private int? StepAt(Point at)
    {
        var width = ColumnWidth;
        var step = width > 0 ? (int)Math.Floor(at.X / width) : -1;

        return step >= 0 && step < Scene.Steps.Count && at.Y >= 0 && at.Y < SequencerLayout.RulerHeight + SequencerLayout.MovementHeight ? step : null;
    }
}
