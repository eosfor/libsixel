using System.Text;
using LibSixel.Internal;

namespace LibSixel;

/// <summary>
/// High-level SIXEL encoder.
/// </summary>
public sealed class SixelEncoder : IDisposable
{
    // -----------------------------------------------------------------------
    // Static convenience methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes <paramref name="pixels"/> to a SIXEL string.
    /// </summary>
    /// <param name="pixels">Raw pixel bytes.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="format">Pixel memory layout.</param>
    /// <param name="dither">Colour palette / quantization settings.</param>
    /// <returns>SIXEL-encoded string starting with ESC P and ending with ESC \.</returns>
    public static string EncodeToString(
        byte[] pixels, int width, int height,
        PixelFormat format, SixelDither dither)
    {
        byte[] bytes = EncodeToBytes(pixels, width, height, format, dither);
        return Encoding.Latin1.GetString(bytes);
    }

    /// <summary>
    /// Encodes <paramref name="pixels"/> to a SIXEL byte array.
    /// </summary>
    public static byte[] EncodeToBytes(
        byte[] pixels, int width, int height,
        PixelFormat format, SixelDither dither)
    {
        var result = new List<byte>(4096);
        using var output = SixelOutput.Create(span =>
        {
            foreach (byte b in span) result.Add(b);
        });
        Encode(pixels, width, height, format, dither, output);
        output.Flush();
        return result.ToArray();
    }

    /// <summary>
    /// Encodes <paramref name="pixels"/> and writes the SIXEL data to <paramref name="output"/>.
    /// </summary>
    /// <param name="pixels">Raw pixel bytes.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="format">Pixel memory layout.</param>
    /// <param name="dither">Colour palette / quantization settings.</param>
    /// <param name="output">Destination output context.</param>
    public static void Encode(
        byte[] pixels, int width, int height,
        PixelFormat format, SixelDither dither,
        SixelOutput output)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(dither);
        ArgumentNullException.ThrowIfNull(output);

        if (width <= 0 || width > SixelConstants.WidthLimit)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0 || height > SixelConstants.HeightLimit)
            throw new ArgumentOutOfRangeException(nameof(height));

        // Apply palette / dithering
        byte[] indexed = dither.ApplyPalette(pixels, width, height);

        // Propagate body-only flag
        output.BodyOnly = dither.BodyOnly;

        SixelCore.Encode(indexed, width, height, dither.Palette, dither.NumColors, output);
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
