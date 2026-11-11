using System.Runtime.InteropServices;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using SkiaSharp;

namespace D3B.Extractor.WorldMaps;

/// <summary>Turns a world map tile of the client, a BC7 texture, into the JPEG the data folder holds (docs/data.md).</summary>
internal static class TileEncoder
{
    /// <summary>The JPEG quality, the owner's choice on the C# version: at full size it cannot be told from the source.</summary>
    public const int Quality = 75;

    // BC7 decodes the tiles' opaque texels as alpha 254 a quarter of the time, never less (main:docs/client.md).
    private const int OpaqueAlpha = 254;

    /// <summary>Decodes a BC7 texture and encodes it as a JPEG, upright.</summary>
    /// <exception cref="InvalidDataException">The texture is not opaque, which a JPEG cannot hold.</exception>
    public static byte[] Encode(ReadOnlySpan<byte> blocks, int width, int height)
    {
        Bc7.Decompress<ColorRGBA<byte>, byte>(blocks, width, height, out var decoded);

        return Jpeg(decoded, width, height);
    }

    /// <summary>Encodes RGBA texels, stored bottom row first as Unity stores them, as an upright JPEG.</summary>
    /// <exception cref="InvalidDataException">A texel is not opaque.</exception>
    public static byte[] Jpeg(byte[] texels, int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[texels.Length];

        for (var row = 0; row < height; row++)
        {
            texels.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((height - 1 - row) * stride));
        }

        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            if (pixels[alpha] < OpaqueAlpha)
            {
                throw new InvalidDataException($"A {width}x{height} tile is not opaque, which a JPEG cannot hold.");
            }
        }

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);

        return data.ToArray();
    }
}
