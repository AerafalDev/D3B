using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET.Extra;
using D3B.Extractor.Unity;
using SkiaSharp;

namespace D3B.Extractor.Breeds;

/// <summary>The breeds' heads the client draws, as PNG files.</summary>
/// <remarks>
/// <c>Content/Picto/UI/class_assets_2x.bundle</c> holds <c>Head_&lt;breed&gt;&lt;sex&gt;</c>, 64 px, one level of BC7, the
/// sprite's rectangle the whole texture (3.7.13.17, <c>artifacts/studies/breeds.md</c>): the texture is drawn whole.
/// </remarks>
internal static partial class HeadPictures
{
    /// <summary>The side of a head, in pixels.</summary>
    public const int Size = 64;

    private const int Bc7Format = 25;

    /// <summary>Reads every head of the client, by breed and sex, as a PNG.</summary>
    /// <exception cref="InvalidDataException">The bundle holds something else than heads of one level of BC7 at <see cref="Size"/> px.</exception>
    public static SortedDictionary<(int Breed, int Sex), byte[]> Read(ClientInstall client)
    {
        var path = Path.Combine(client.StreamingAssets, "Content", "Picto", "UI", "class_assets_2x.bundle");

        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The client has no {path}.");
        }

        using var bundle = Bundle.Open(path);
        var heads = new SortedDictionary<(int Breed, int Sex), byte[]>();

        foreach (var asset in bundle.Objects(AssetClassID.Texture2D))
        {
            var reader = new ObjectReader(asset.Data, asset.Tree);
            var texture = Fields.Read(ref reader, 0, asset.Tree.Name);
            var name = texture.String("m_Name");

            if (Name().Match(name) is not { Success: true } match)
            {
                throw new InvalidDataException($"{path} holds the texture {name}, which is no breed's head.");
            }

            var head = (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));

            if (!heads.TryAdd(head, Png(Texels(bundle, name, texture), Size, Size)))
            {
                throw new InvalidDataException($"{path} holds two textures named {name}.");
            }
        }

        return heads;
    }

    /// <summary>Decodes a BC7 texture and encodes it as a PNG, upright, its transparency kept.</summary>
    public static byte[] Png(ReadOnlySpan<byte> blocks, int width, int height)
    {
        Bc7.Decompress<ColorRGBA<byte>, byte>(blocks, width, height, out var texels);

        // Unity stores the bottom row first.
        var stride = width * 4;
        var pixels = new byte[texels.Length];

        for (var row = 0; row < height; row++)
        {
            texels.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((height - 1 - row) * stride));
        }

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    private static byte[] Texels(Bundle bundle, string name, Fields texture)
    {
        var stream = texture.Nested("m_StreamData");
        const int size = Size / 4 * (Size / 4) * 16;

        if (texture.Int32("m_TextureFormat") != Bc7Format || texture.Int32("m_MipCount") != 1 || texture.Int32("m_ImageCount") != 1
            || texture.Int32("m_Width") != Size || texture.Int32("m_Height") != Size || stream.UInt32("size") != size)
        {
            throw new InvalidDataException($"The head {name} is {texture.Int32("m_Width")}x{texture.Int32("m_Height")}, format {texture.Int32("m_TextureFormat")}: not one level of BC7 at {Size} px.");
        }

        return bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), size);
    }

    // The sex is the last digit: Head_10 is breed 1's male, Head_100 breed 10's male.
    [GeneratedRegex(@"^Head_([1-9][0-9]*)([01])$", RegexOptions.CultureInvariant)]
    private static partial Regex Name();
}
