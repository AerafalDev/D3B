namespace D3B.Engine.Fights;

/// <summary>A cast's way through an Eliotrope's portals, as <see cref="Portals.Through"/> finds it.</summary>
/// <param name="Entry">The portal's cell cast on.</param>
/// <param name="Exit">The cell of the portal it comes out of, which its zone and its pushes start from.</param>
/// <param name="Target">The cell its effects land on.</param>
/// <param name="Bonus">The share, in percent, its effects gain.</param>
public readonly record struct PortalPassage(int Entry, int Exit, int Target, int Bonus);
