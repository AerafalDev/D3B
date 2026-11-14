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
/// 21, BC7 at 32 px (3.7.13.17, `artifacts/studies/items.md`). A texture is drawn whole, scaled to
/// <see cref="Size"/>: the client draws an item's sprite over its whole texture (the C# version, inferred for 3.7.13.17).
/// </remarks>
internal static class IconPictures
{
    /// <summary>The side of an icon, in pixels: every item's at 32 px, the owner's choice of 2026-11-14.</summary>
    public const int Size = 32;

    private const int Rgba32Format = 4;
    private const int Bc7Format = 25;

    /// <summary>Reads every icon of the bundle at <paramref name="path"/>, relative to the client's streaming assets.</summary>
    /// <exception cref="InvalidDataException">The bundle holds a texture not named by a number, two of one name, or one <see cref="Png"/> refuses.</exception>
    public static SortedDictionary<int, byte[]> Read(ClientInstall client, params string[] path)
    {
        using var bundle = Bundle.Open(Path.Combine([client.StreamingAssets, .. path]));
        var icons = new SortedDictionary<int, byte[]>();

        foreach (var asset in bundle.Objects(AssetClassID.Texture2D))
        {
            var reader = new ObjectReader(asset.Data, asset.Tree);
            var texture = Fields.Read(ref reader, 0, asset.Tree.Name);
            var name = texture.String("m_Name");
            var icon = IconOf(name) ?? throw new InvalidDataException($"{bundle.Path} holds the texture {name}, which is no icon's number.");
            var (format, side, levels) = (texture.Int32("m_TextureFormat"), texture.Int32("m_Width"), texture.Int32("m_MipCount"));

            if (texture.Int32("m_Height") != side || levels != 1 || texture.Int32("m_ImageCount") != 1)
            {
                throw new InvalidDataException($"The icon {name} is {side}x{texture.Int32("m_Height")} in {levels} levels: not one square level.");
            }

            var stream = texture.Nested("m_StreamData");
            var texels = bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), checked((int)stream.UInt32("size")));

            if (!icons.TryAdd(icon, Png(texels, format, side)))
            {
                throw new InvalidDataException($"{bundle.Path} holds two textures named {name}.");
            }
        }

        return icons;
    }

    /// <summary>Reads an icon's texture name, its number written plainly, such as <c>0</c> or <c>25257</c>.</summary>
    /// <returns>The number, or <see langword="null"/> for a name of another shape.</returns>
    public static int? IconOf(string name)
    {
        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var icon)
            && icon.ToString(CultureInfo.InvariantCulture) == name ? icon : null;
    }

    /// <summary>Decodes a square texture of one level, BC7 or RGBA32, and encodes it as a PNG of <see cref="Size"/> px, upright, its transparency kept.</summary>
    /// <exception cref="InvalidDataException">The texture is of another format, smaller than <see cref="Size"/>, or its bytes are not its side's.</exception>
    public static byte[] Png(ReadOnlySpan<byte> data, int format, int side)
    {
        var blocks = (side + 3) / 4;
        var expected = format switch
        {
            Bc7Format => blocks * blocks * 16,
            Rgba32Format => side * side * 4,
            _ => -1,
        };

        if (expected < 0 || side < Size || data.Length != expected)
        {
            throw new InvalidDataException($"An icon of {side} px in format {format} holds {data.Length} bytes: not BC7 or RGBA32 of at least {Size} px.");
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

        // Unity stores the bottom row first.
        var stride = side * 4;
        var pixels = new byte[texels.Length];

        for (var row = 0; row < side; row++)
        {
            texels.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((side - 1 - row) * stride));
        }

        if (side == Size)
        {
            using var kept = Image(pixels, side, SKAlphaType.Unpremul);

            return Encode(kept);
        }

        // Skia filters the texels as they are stored: premultiplied, a transparent texel's colour does not darken its
        // neighbours. Linear sampling averages each 2x2 square of a 64 px icon and blends the few of 60 and 62 px.
        for (var texel = 0; texel < pixels.Length; texel += 4)
        {
            for (var channel = texel; channel < texel + 3; channel++)
            {
                pixels[channel] = (byte)(((pixels[channel] * pixels[texel + 3]) + 127) / 255);
            }
        }

        using var source = Image(pixels, side, SKAlphaType.Premul);
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));

        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(source, SKRect.Create(side, side), SKRect.Create(Size, Size), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), null);

        using var scaled = surface.Snapshot();

        return Encode(scaled);
    }

    private static SKImage Image(byte[] pixels, int side, SKAlphaType alpha)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(side, side, SKColorType.Rgba8888, alpha));

        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        return SKImage.FromBitmap(bitmap);
    }

    private static byte[] Encode(SKImage image)
    {
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        return png.ToArray();
    }
}
