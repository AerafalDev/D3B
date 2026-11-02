namespace D3B.Scripts.Icons;

/// <summary>One shape of an icon: its outline as a path, what fills it and what strokes it, on the icon's grid of 16.</summary>
/// <param name="Data">The outline, in Avalonia's path syntax, its fill rule first.</param>
/// <param name="Transform">The matrix the outline is drawn through, <c>m11,m12,m21,m22,dx,dy</c>, or <see langword="null"/>.</param>
/// <param name="Fill">The fill, <c>#AARRGGBB</c>, or <see langword="null"/>.</param>
/// <param name="Stroke">The stroke, <c>#AARRGGBB</c>, or <see langword="null"/>.</param>
/// <param name="StrokeWidth">The stroke's width.</param>
/// <param name="LineCap">The stroke's ends: <c>Flat</c>, <c>Round</c> or <c>Square</c>.</param>
/// <param name="LineJoin">The stroke's corners: <c>Miter</c>, <c>Round</c> or <c>Bevel</c>.</param>
internal sealed record IconShape(string Data, string? Transform, string? Fill, string? Stroke, double StrokeWidth, string LineCap, string LineJoin);
