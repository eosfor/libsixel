using System.Text;
using LibSixel.Internal;

namespace LibSixel;

/// <summary>
/// High-level SIXEL decoder.
/// </summary>
public sealed class SixelDecoder
{
    /// <summary>
    /// Decodes a SIXEL byte array.
    /// </summary>
    /// <param name="sixelData">Raw SIXEL bytes, typically starting with <c>ESC P</c>.</param>
    /// <returns>Decoded image result.</returns>
    public static SixelDecodeResult Decode(byte[] sixelData)
    {
        ArgumentNullException.ThrowIfNull(sixelData);
        return SixelParser.Decode(sixelData);
    }

    /// <summary>
    /// Decodes a SIXEL string.
    /// </summary>
    /// <param name="sixelString">SIXEL data as a string (Latin-1 / ISO-8859-1 encoding assumed).</param>
    public static SixelDecodeResult Decode(string sixelString)
    {
        ArgumentNullException.ThrowIfNull(sixelString);
        byte[] bytes = Encoding.Latin1.GetBytes(sixelString);
        return SixelParser.Decode(bytes);
    }

    /// <summary>
    /// Decodes a SIXEL byte span.
    /// </summary>
    public static SixelDecodeResult Decode(ReadOnlySpan<byte> sixelData)
        => SixelParser.Decode(sixelData);
}
