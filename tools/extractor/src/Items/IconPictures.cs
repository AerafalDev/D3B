using System.Globalization;
using System.Runtime.InteropServices;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET.Extra;
using D3B.Extractor.Unity;
using SkiaSharp;

namespace D3B.Extractor.Items;

/// <summary>The icons of a bundle of the client, by number, as PNG files of <see cref="Size"/> px.</summary>
/// <remarks>
/// <c>Content/Picto/Items/item_assets_1x.bundle</c> holds 12,295 textures named by the icon's number, one level each:
/// BC7 at 64 px but 82,523 and 121,179 at 60 px, and 25,257 RGBA32 at 62 px; <c>Content/Picto/UI/job_assets_1x.bundle</c>
/// 21, BC7 at 32 px (3.7.13.17, <c>artifacts/studies/items.md</c>). A texture is drawn whole, scaled to
/// <see cref="Size"/>: the client draws an item's sprite over its whole texture (the C# version, inferred for 3.7.13.17).
/// </remarks>
internal static class IconPictures
{
    /// <summary>The side of an icon, in pixels: every item's at 32 px, the owner's choice of 2026-11-14.</summary>
    public const int Size = 32;

    /// <summary>The largest side of a texture the extractor scales: 3.7.13.17's item icons are 64 px or a little less.</summary>
    public const int Largest = 64;

    /// <summary>Unity's <c>RGBA32</c>, one item icon of 3.7.13.17.</summary>
    public const int Rgba32Format = 4;

    /// <summary>Unity's <c>BC7</c>, every other icon.</summary>
    public const int Bc7Format = 25;

    /// <summary>Reads every icon of the bundle at <paramref name="path"/>, relative to the client's streaming assets.</summary>
    /// <param name="client">The client.</param>
    /// <param name="largest">The largest side a texture may have, from <see cref="Size"/>.</param>
    /// <param name="formats">The formats a texture may be in.</param>
    /// <param name="path">The bundle's path, folder by folder.</param>
    /// <exception cref="InvalidDataException">A texture is not what <see cref="CheckShape"/>, <see cref="Add"/> or <see cref="Png"/> accept.</exception>
    public static SortedDictionary<int, byte[]> Read(ClientInstall client, int largest, IReadOnlyCollection<int> formats, params string[] path)
    {
        using var bundle = Bundle.Open(Path.Combine([client.StreamingAssets, .. path]));
        var icons = new SortedDictionary<int, byte[]>();

        foreach (var asset in bundle.Objects(AssetClassID.Texture2D))
        {
            var reader = new ObjectReader(asset.Data, asset.Tree);
            var texture = Fields.Read(ref reader, 0, asset.Tree.Name);
            var name = texture.String("m_Name");
            var (format, side) = (texture.Int32("m_TextureFormat"), texture.Int32("m_Width"));

            CheckShape(name, format, side, texture.Int32("m_Height"), texture.Int32("m_MipCount"), texture.Int32("m_ImageCount"), largest, formats);

            var stream = texture.Nested("m_StreamData");
            var texels = bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), checked((int)stream.UInt32("size")));

            Add(icons, bundle.Path, name, Png(texels, format, side));
        }

        return icons;
    }

    /// <summary>Checks that a texture is one square level of one of <paramref name="formats"/>, from <see cref="Size"/> to <paramref name="largest"/> px a side.</summary>
    /// <exception cref="InvalidDataException">It is not: a larger one would be scaled by more than half, which the extractor does not measure.</exception>
    public static void CheckShape(string name, int format, int width, int height, int levels, int images, int largest, IReadOnlyCollection<int> formats)
    {
        if (width != height || levels != 1 || images != 1 || width < Size || width > largest || !formats.Contains(format))
        {
            throw new InvalidDataException($"The icon {name} is {width}x{height} in format {format}, {levels} levels, {images} images: not one square level of {Size} to {largest} px in format {string.Join(" or ", formats)}.");
        }
    }

    /// <summary>Adds the icon a texture named <paramref name="name"/> draws.</summary>
    /// <exception cref="InvalidDataException">The name is no icon's number, or another texture of <paramref name="bundle"/> had it.</exception>
    public static void Add(SortedDictionary<int, byte[]> icons, string bundle, string name, byte[] png)
    {
        var icon = IconOf(name) ?? throw new InvalidDataException($"{bundle} holds the texture {name}, which is no icon's number.");

        if (!icons.TryAdd(icon, png))
        {
            throw new InvalidDataException($"{bundle} holds two textures named {name}.");
        }
    }

    /// <summary>Reads an icon's texture name, its number written plainly, such as <c>0</c> or <c>25257</c>.</summary>
    /// <returns>The number, or <see langword="null"/> for a name of another shape.</returns>
    public static int? IconOf(string name)
    {
        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var icon)
            && icon.ToString(CultureInfo.InvariantCulture) == name ? icon : null;
    }

    /// <summary>Decodes a square texture of one level, BC7 or RGBA32, and encodes it as a PNG of <see cref="Size"/> px, upright, its transparency kept.</summary>
    /// <exception cref="InvalidDataException">The texture is of another format, outside <see cref="Size"/> to <see cref="Largest"/> px, or its bytes are not its side's.</exception>
    public static byte[] Png(ReadOnlySpan<byte> data, int format, int side)
    {
        var blocks = (side + 3) / 4;
        var expected = format switch
        {
            Bc7Format => blocks * blocks * 16,
            Rgba32Format => side * side * 4,
            _ => -1,
        };

        if (expected < 0 || side is < Size or > Largest || data.Length != expected)
        {
            throw new InvalidDataException($"An icon of {side} px in format {format} holds {data.Length} bytes: not BC7 or RGBA32 of {Size} to {Largest} px.");
        }

        byte[] texels;

        if (format == Bc7Format)
        {
            Bc7.Decompress<ColorRGBA<byte>, byte>(data, side, side, out texels);
        }
        else
        {
            texels = data.ToArray();
        }

        var pixels = Scaled(texels, side);

        using var bitmap = new SKBitmap(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        return png.ToArray();
    }

    // Each icon texel is the mean of the texels it covers, weighted by their alpha, so that a transparent texel's colour
    // does not tint its neighbours: a 64 px icon's 2x2 squares, a 32 px icon's texels as they are, the few of 60 and 62
    // px by the part of each texel covered. Done by hand rather than by Skia, so that every CPU gives the same bytes.
    private static byte[] Scaled(byte[] texels, int side)
    {
        var scale = (double)side / Size;
        var pixels = new byte[Size * Size * 4];

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                double red = 0, green = 0, blue = 0, alpha = 0;

                for (var sy = (int)(y * scale); sy < Math.Min(side, (int)Math.Ceiling((y + 1) * scale)); sy++)
                {
                    for (var sx = (int)(x * scale); sx < Math.Min(side, (int)Math.Ceiling((x + 1) * scale)); sx++)
                    {
                        // Unity stores the bottom row first.
                        var at = ((((side - 1 - sy) * side) + sx) * 4);
                        var weight = Covered(y, sy, scale) * Covered(x, sx, scale) * texels[at + 3];

                        (red, green, blue, alpha) = (red + (weight * texels[at]), green + (weight * texels[at + 1]), blue + (weight * texels[at + 2]), alpha + weight);
                    }
                }

                var to = ((y * Size) + x) * 4;

                pixels[to + 3] = Rounded(alpha / (scale * scale));

                if (alpha > 0)
                {
                    (pixels[to], pixels[to + 1], pixels[to + 2]) = (Rounded(red / alpha), Rounded(green / alpha), Rounded(blue / alpha));
                }
            }
        }

        return pixels;
    }

    // How much of source texel `source` the icon texel `target` covers, along one axis.
    private static double Covered(int target, int source, double scale)
    {
        return Math.Min(source + 1, (target + 1) * scale) - Math.Max(source, target * scale);
    }

    private static byte Rounded(double value)
    {
        return (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
    }
}
