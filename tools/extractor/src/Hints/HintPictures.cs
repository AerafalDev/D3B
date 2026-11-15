using System.Runtime.InteropServices;
using AssetRipper.TextureDecoder.Dxt;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET.Extra;
using D3B.Extractor.Items;
using D3B.Extractor.Unity;
using SkiaSharp;

namespace D3B.Extractor.Hints;

/// <summary>The hints' pictures, by number, as PNG files at the client's size (the owner's choice of 2026-11-15).</summary>
/// <remarks>
/// <c>aa/&lt;platform&gt;/uidarkstone_assets_all.bundle</c> holds the textures named by a hint's <c>gfx</c>, among the
/// interface's others: DXT5 at 64 px, streamed but for 470 held in the object, and the class places' RGBA32 at 62 px in
/// the object; one level each (3.7.13.17, <c>main:docs/client.md</c>, "Hints").
/// </remarks>
internal static class HintPictures
{
    /// <summary>Unity's <c>RGBA32</c>: the class places' pictures.</summary>
    public const int Rgba32Format = 4;

    /// <summary>Unity's <c>DXT5</c>: every other picture.</summary>
    public const int Dxt5Format = 12;

    /// <summary>Reads the pictures of <paramref name="graphics"/> the client holds, leaving out the others.</summary>
    /// <exception cref="InvalidDataException">The client holds no bundle of pictures, or one of them is not as measured.</exception>
    public static SortedDictionary<int, byte[]> Read(ClientInstall client, IReadOnlySet<int> graphics)
    {
        var root = Path.Combine(client.StreamingAssets, "aa");
        var paths = Directory.Exists(root) ? Directory.GetFiles(root, "uidarkstone_assets_all.bundle", SearchOption.AllDirectories) : [];

        if (paths.Length is not 1)
        {
            throw new InvalidDataException($"The client holds {paths.Length} uidarkstone_assets_all.bundle under {root}, where the extractor reads one.");
        }

        using var bundle = Bundle.Open(paths[0]);
        var pictures = new SortedDictionary<int, byte[]>();

        foreach (var asset in bundle.Objects(AssetClassID.Texture2D))
        {
            var reader = new ObjectReader(asset.Data, asset.Tree);
            var texture = Fields.Read(ref reader, 0, asset.Tree.Name);
            var name = texture.String("m_Name");

            if (IconPictures.IconOf(name) is not { } gfx || !graphics.Contains(gfx))
            {
                continue;
            }

            var (format, side) = (texture.Int32("m_TextureFormat"), texture.Int32("m_Width"));

            CheckShape(name, format, side, texture.Int32("m_Height"), texture.Int32("m_MipCount"), texture.Int32("m_ImageCount"));

            var stream = texture.Nested("m_StreamData");
            var size = stream.UInt32("size");
            var texels = size is 0
                ? texture.Bytes("image data")
                : bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), checked((int)size));

            if (!pictures.TryAdd(gfx, Png(texels, format, side)))
            {
                throw new InvalidDataException($"{bundle.Path} holds two textures named {name}.");
            }
        }

        return pictures;
    }

    /// <summary>Checks that a hint's texture is one square level of DXT5 at 64 px or RGBA32 at 62 px.</summary>
    /// <exception cref="InvalidDataException">It is not.</exception>
    public static void CheckShape(string name, int format, int width, int height, int levels, int images)
    {
        var measured = (format, width) is (Dxt5Format, 64) or (Rgba32Format, 62);

        if (!measured || width != height || levels != 1 || images != 1)
        {
            throw new InvalidDataException($"The hint picture {name} is {width}x{height} in format {format}, {levels} levels, {images} images: not one level of DXT5 at 64 px or RGBA32 at 62 px.");
        }
    }

    /// <summary>Decodes a square texture of one level, DXT5 or RGBA32, and encodes it as a PNG of its size, upright, its transparency kept.</summary>
    /// <exception cref="InvalidDataException">The texture is of another format, or its bytes are not its side's.</exception>
    public static byte[] Png(ReadOnlySpan<byte> data, int format, int side)
    {
        var blocks = (side + 3) / 4;
        var expected = format switch
        {
            Dxt5Format => blocks * blocks * 16,
            Rgba32Format => side * side * 4,
            _ => -1,
        };

        if (expected < 0 || data.Length != expected)
        {
            throw new InvalidDataException($"A hint picture of {side} px in format {format} holds {data.Length} bytes: not DXT5 or RGBA32 of its side.");
        }

        byte[] texels;

        if (format == Dxt5Format)
        {
            DxtDecoder.DecompressDXT5<ColorRGBA<byte>, byte>(data, side, side, out texels);
        }
        else
        {
            texels = data.ToArray();
        }

        // Unity stores the bottom row first.
        var stride = side * 4;
        var pixels = new byte[stride * side];

        for (var row = 0; row < side; row++)
        {
            texels.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((side - 1 - row) * stride));
        }

        using var bitmap = new SKBitmap(new SKImageInfo(side, side, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        return png.ToArray();
    }
}
