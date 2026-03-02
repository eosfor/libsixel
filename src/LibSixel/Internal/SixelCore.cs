namespace LibSixel.Internal;

/// <summary>
/// Core SIXEL encoding algorithm, ported from <c>tosixel.c</c> in the original libsixel C library.
/// </summary>
internal static class SixelCore
{
    // DCS introducer / string terminator (7-bit)
    private static ReadOnlySpan<byte> DcsIntro => "\x1bP"u8;
    private static ReadOnlySpan<byte> St => "\x1b\\"u8;

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes a palette-indexed image to SIXEL format and writes it to <paramref name="output"/>.
    /// </summary>
    /// <param name="palettedPixels">One palette-index byte per pixel, in row-major order.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="palette">Flat RGB24 palette (length = ncolors × 3).</param>
    /// <param name="ncolors">Number of colours in the palette.</param>
    /// <param name="output">Output context to write into.</param>
    public static void Encode(
        byte[] palettedPixels,
        int width, int height,
        byte[] palette, int ncolors,
        SixelOutput output)
    {
        if (!output.BodyOnly)
            WriteHeader(width, height, palette, ncolors, output);

        WriteBody(palettedPixels, width, height, palette, ncolors, output);

        if (!output.BodyOnly)
            WriteFooter(output);
    }

    // -----------------------------------------------------------------------
    // Header
    // -----------------------------------------------------------------------

    private static void WriteHeader(int width, int height, byte[] palette, int ncolors, SixelOutput output)
    {
        if (output.PenetrateMultiplexer)
        {
            // GNU Screen pass-through wrapper: ESC P ... ESC \
            output.WriteBytes("\x1bP0;0;0q"u8);
        }

        if (output.Has8BitControls)
        {
            // 8-bit DCS (0x90)
            output.WriteByte(0x90);
        }
        else
        {
            output.WriteBytes(DcsIntro);
        }

        // P1 (pixel aspect ratio) = 0 (default), P2 (background color) = 1 (background stays), P3 (grid size) = 0
        output.WriteBytes("0;1;0q"u8);

        // Raster attributes: "Pan;Pad;Ph;Pv
        output.WriteByte((byte)'"');
        output.WriteNumber(1); // pan
        output.WriteByte((byte)';');
        output.WriteNumber(1); // pad
        output.WriteByte((byte)';');
        output.WriteNumber(width);
        output.WriteByte((byte)';');
        output.WriteNumber(height);
    }

    // -----------------------------------------------------------------------
    // Body
    // -----------------------------------------------------------------------

    private static void WriteBody(
        byte[] palettedPixels,
        int width, int height,
        byte[] palette, int ncolors,
        SixelOutput output)
    {
        // For each 6-row band
        int bands = (height + 5) / 6;

        // Reusable buffer: one sixel character per column per colour
        var sixels = new byte[width];

        for (int band = 0; band < bands; band++)
        {
            int yBase = band * 6;
            bool lastBand = band == bands - 1;

            // Determine which colours actually appear in this band
            bool[] colorUsed = new bool[ncolors];
            for (int row = 0; row < 6; row++)
            {
                int y = yBase + row;
                if (y >= height) break;
                for (int x = 0; x < width; x++)
                {
                    colorUsed[palettedPixels[y * width + x]] = true;
                }
            }

            bool firstColorWritten = false;

            for (int c = 0; c < ncolors; c++)
            {
                if (!colorUsed[c]) continue;

                // Write colour selector + definition
                WriteColorIntro(c, palette, output);

                // Build sixel characters for each column
                for (int x = 0; x < width; x++)
                {
                    byte bits = 0;
                    for (int row = 0; row < 6; row++)
                    {
                        int y = yBase + row;
                        if (y >= height) break;
                        if (palettedPixels[y * width + x] == c)
                            bits |= (byte)(1 << row);
                    }
                    sixels[x] = (byte)('?' + bits);
                }

                // Write with RLE
                WriteRle(sixels, width, output);

                firstColorWritten = true;

                // Determine whether this is the last colour with data in this band
                bool isLastColorInBand = true;
                for (int cc = c + 1; cc < ncolors; cc++)
                {
                    if (colorUsed[cc]) { isLastColorInBand = false; break; }
                }

                if (isLastColorInBand)
                {
                    // End of band: DECGNL (new line)
                    output.WriteByte((byte)'-');
                }
                else
                {
                    // More colours to write for this band: DECGCR (carriage return)
                    output.WriteByte((byte)'$');
                }
            }

            // If no colour was written for this band (blank band), still advance
            if (!firstColorWritten)
            {
                output.WriteByte((byte)'-');
            }
        }
    }

    /// <summary>
    /// Writes <c>#index;2;r;g;b</c> colour-introduction sequence.
    /// r, g, b values are scaled to 0–100 as required by the SIXEL spec.
    /// </summary>
    private static void WriteColorIntro(int index, byte[] palette, SixelOutput output)
    {
        output.WriteByte((byte)'#');
        output.WriteNumber(index);
        output.WriteByte((byte)';');
        output.WriteNumber(2); // RGB
        output.WriteByte((byte)';');
        output.WriteNumber((palette[index * 3 + 0] * 100 + 127) / 255);
        output.WriteByte((byte)';');
        output.WriteNumber((palette[index * 3 + 1] * 100 + 127) / 255);
        output.WriteByte((byte)';');
        output.WriteNumber((palette[index * 3 + 2] * 100 + 127) / 255);
    }

    /// <summary>
    /// Writes <paramref name="sixels"/> using repeat encoding where beneficial
    /// (<c>!count char</c> when the same character repeats more than 3 times).
    /// </summary>
    private static void WriteRle(byte[] sixels, int width, SixelOutput output)
    {
        int i = 0;
        while (i < width)
        {
            byte ch = sixels[i];
            int run = 1;
            while (i + run < width && sixels[i + run] == ch && run < 32767)
                run++;
            output.WriteRepeat(ch, run);
            i += run;
        }
    }

    // -----------------------------------------------------------------------
    // Footer
    // -----------------------------------------------------------------------

    private static void WriteFooter(SixelOutput output)
    {
        if (output.Has8BitControls)
            output.WriteByte(0x9C); // 8-bit ST
        else
            output.WriteBytes(St);
    }
}
