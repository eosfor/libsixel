namespace LibSixel.Internal;

/// <summary>
/// Median-cut colour quantizer and palette-application helper.
/// Ported from quant.c in the original libsixel C library.
/// </summary>
internal static class ColorQuantizer
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Quantizes the colours in <paramref name="rgbPixels"/> using the median-cut algorithm
    /// and returns an array of paletted pixel indices.
    /// </summary>
    /// <param name="rgbPixels">Packed RGB24 bytes (R, G, B per pixel).</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="maxColors">Desired palette size (2–256).</param>
    /// <param name="palette">Receives the RGB24 palette (length = actual colour count × 3).</param>
    /// <returns>Array of palette indices, one per pixel (length = width × height).</returns>
    public static byte[] Quantize(
        ReadOnlySpan<byte> rgbPixels,
        int width, int height,
        int maxColors,
        out byte[] palette)
    {
        int nPixels = width * height;

        // --- 1. Build a colour histogram ---
        // Key = packed 15-bit colour (5 bits per channel), Value = count
        var hist = new Dictionary<int, int>(65536);
        for (int i = 0; i < nPixels; i++)
        {
            int r = rgbPixels[i * 3 + 0] >> 3;
            int g = rgbPixels[i * 3 + 1] >> 3;
            int b = rgbPixels[i * 3 + 2] >> 3;
            int key = (r << 10) | (g << 5) | b;
            hist.TryGetValue(key, out int cnt);
            hist[key] = cnt + 1;
        }

        // Convert histogram to a list of (R, G, B, count) entries
        var colors = new List<ColorEntry>(hist.Count);
        foreach (var kv in hist)
        {
            int key = kv.Key;
            colors.Add(new ColorEntry
            {
                R = (byte)(((key >> 10) & 0x1F) << 3),
                G = (byte)(((key >> 5) & 0x1F) << 3),
                B = (byte)((key & 0x1F) << 3),
                Count = kv.Value,
            });
        }

        // --- 2. Median-cut ---
        var boxes = new List<Box> { new Box { Start = 0, End = colors.Count } };
        colors.Sort(Comparison_R);  // initial sort

        while (boxes.Count < maxColors)
        {
            // Find the box with the largest colour range
            int splitIdx = FindBoxToSplit(colors, boxes);
            if (splitIdx < 0) break;

            Box box = boxes[splitIdx];
            boxes.RemoveAt(splitIdx);

            SplitBox(colors, box, out Box b1, out Box b2);
            boxes.Add(b1);
            boxes.Add(b2);
        }

        // --- 3. Choose representative colour for each box ---
        palette = new byte[boxes.Count * 3];
        for (int bi = 0; bi < boxes.Count; bi++)
        {
            GetBoxRepresentative(colors, boxes[bi], out byte r, out byte g, out byte b);
            palette[bi * 3 + 0] = r;
            palette[bi * 3 + 1] = g;
            palette[bi * 3 + 2] = b;
        }

        // --- 4. Map each pixel to nearest palette entry (no dithering here) ---
        return MapPixelsToNearestColor(rgbPixels, nPixels, palette, boxes.Count);
    }

    /// <summary>
    /// Maps each pixel in <paramref name="rgbPixels"/> to the nearest entry in
    /// <paramref name="palette"/>, optionally applying error diffusion.
    /// </summary>
    /// <param name="rgbPixels">Packed RGB24 source pixels.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="palette">RGB24 palette.</param>
    /// <param name="ncolors">Number of palette entries.</param>
    /// <param name="diffuseMethod">Dithering method (see <see cref="SixelConstants.DiffuseNone"/> etc.).</param>
    /// <returns>Palette-indexed pixel array.</returns>
    public static byte[] ApplyPalette(
        ReadOnlySpan<byte> rgbPixels,
        int width, int height,
        byte[] palette,
        int ncolors,
        int diffuseMethod)
    {
        if (diffuseMethod == SixelConstants.DiffuseNone || diffuseMethod == SixelConstants.DiffuseAuto)
            return MapPixelsToNearestColor(rgbPixels, width * height, palette, ncolors);

        return DiffusePalette(rgbPixels, width, height, palette, ncolors, diffuseMethod);
    }

    // -----------------------------------------------------------------------
    // Nearest-colour mapping
    // -----------------------------------------------------------------------

    private static byte[] MapPixelsToNearestColor(ReadOnlySpan<byte> rgb, int nPixels, byte[] palette, int ncolors)
    {
        var result = new byte[nPixels];
        for (int i = 0; i < nPixels; i++)
        {
            result[i] = (byte)NearestColor(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2], palette, ncolors);
        }
        return result;
    }

    private static int NearestColor(byte r, byte g, byte b, byte[] palette, int ncolors)
    {
        int best = 0;
        int bestDist = int.MaxValue;
        for (int i = 0; i < ncolors; i++)
        {
            int dr = r - palette[i * 3 + 0];
            int dg = g - palette[i * 3 + 1];
            int db = b - palette[i * 3 + 2];
            int dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
                if (dist == 0) break;
            }
        }
        return best;
    }

    // -----------------------------------------------------------------------
    // Error-diffusion dithering
    // -----------------------------------------------------------------------

    private static byte[] DiffusePalette(
        ReadOnlySpan<byte> rgbPixels,
        int width, int height,
        byte[] palette, int ncolors,
        int method)
    {
        // Working buffer of float errors (per channel)
        var errR = new float[width * height];
        var errG = new float[width * height];
        var errB = new float[width * height];
        var result = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float r = Clamp(rgbPixels[idx * 3 + 0] + errR[idx]);
                float g = Clamp(rgbPixels[idx * 3 + 1] + errG[idx]);
                float b = Clamp(rgbPixels[idx * 3 + 2] + errB[idx]);

                int c = NearestColor((byte)r, (byte)g, (byte)b, palette, ncolors);
                result[idx] = (byte)c;

                float er = r - palette[c * 3 + 0];
                float eg = g - palette[c * 3 + 1];
                float eb = b - palette[c * 3 + 2];

                DiffuseError(errR, errG, errB, width, height, x, y, er, eg, eb, method);
            }
        }
        return result;
    }

    private static void DiffuseError(
        float[] errR, float[] errG, float[] errB,
        int width, int height,
        int x, int y,
        float er, float eg, float eb,
        int method)
    {
        // Error diffusion matrices
        // Each entry: (dx, dy, numerator, denominator)
        switch (method)
        {
            case SixelConstants.DiffuseFloydSteinberg:
                Spread(errR, errG, errB, width, height, x + 1, y + 0, er, eg, eb, 7, 16);
                Spread(errR, errG, errB, width, height, x - 1, y + 1, er, eg, eb, 3, 16);
                Spread(errR, errG, errB, width, height, x + 0, y + 1, er, eg, eb, 5, 16);
                Spread(errR, errG, errB, width, height, x + 1, y + 1, er, eg, eb, 1, 16);
                break;

            case SixelConstants.DiffuseAtkinson:
                Spread(errR, errG, errB, width, height, x + 1, y + 0, er, eg, eb, 1, 8);
                Spread(errR, errG, errB, width, height, x + 2, y + 0, er, eg, eb, 1, 8);
                Spread(errR, errG, errB, width, height, x - 1, y + 1, er, eg, eb, 1, 8);
                Spread(errR, errG, errB, width, height, x + 0, y + 1, er, eg, eb, 1, 8);
                Spread(errR, errG, errB, width, height, x + 1, y + 1, er, eg, eb, 1, 8);
                Spread(errR, errG, errB, width, height, x + 0, y + 2, er, eg, eb, 1, 8);
                break;

            case SixelConstants.DiffuseJarvisJudiceNinke:
                Spread(errR, errG, errB, width, height, x + 1, y + 0, er, eg, eb, 7, 48);
                Spread(errR, errG, errB, width, height, x + 2, y + 0, er, eg, eb, 5, 48);
                Spread(errR, errG, errB, width, height, x - 2, y + 1, er, eg, eb, 3, 48);
                Spread(errR, errG, errB, width, height, x - 1, y + 1, er, eg, eb, 5, 48);
                Spread(errR, errG, errB, width, height, x + 0, y + 1, er, eg, eb, 7, 48);
                Spread(errR, errG, errB, width, height, x + 1, y + 1, er, eg, eb, 5, 48);
                Spread(errR, errG, errB, width, height, x + 2, y + 1, er, eg, eb, 3, 48);
                Spread(errR, errG, errB, width, height, x - 2, y + 2, er, eg, eb, 1, 48);
                Spread(errR, errG, errB, width, height, x - 1, y + 2, er, eg, eb, 3, 48);
                Spread(errR, errG, errB, width, height, x + 0, y + 2, er, eg, eb, 5, 48);
                Spread(errR, errG, errB, width, height, x + 1, y + 2, er, eg, eb, 3, 48);
                Spread(errR, errG, errB, width, height, x + 2, y + 2, er, eg, eb, 1, 48);
                break;

            case SixelConstants.DiffuseStucki:
                Spread(errR, errG, errB, width, height, x + 1, y + 0, er, eg, eb, 8, 42);
                Spread(errR, errG, errB, width, height, x + 2, y + 0, er, eg, eb, 4, 42);
                Spread(errR, errG, errB, width, height, x - 2, y + 1, er, eg, eb, 2, 42);
                Spread(errR, errG, errB, width, height, x - 1, y + 1, er, eg, eb, 4, 42);
                Spread(errR, errG, errB, width, height, x + 0, y + 1, er, eg, eb, 8, 42);
                Spread(errR, errG, errB, width, height, x + 1, y + 1, er, eg, eb, 4, 42);
                Spread(errR, errG, errB, width, height, x + 2, y + 1, er, eg, eb, 2, 42);
                Spread(errR, errG, errB, width, height, x - 2, y + 2, er, eg, eb, 1, 42);
                Spread(errR, errG, errB, width, height, x - 1, y + 2, er, eg, eb, 2, 42);
                Spread(errR, errG, errB, width, height, x + 0, y + 2, er, eg, eb, 4, 42);
                Spread(errR, errG, errB, width, height, x + 1, y + 2, er, eg, eb, 2, 42);
                Spread(errR, errG, errB, width, height, x + 2, y + 2, er, eg, eb, 1, 42);
                break;

            case SixelConstants.DiffuseBurkes:
                Spread(errR, errG, errB, width, height, x + 1, y + 0, er, eg, eb, 8, 32);
                Spread(errR, errG, errB, width, height, x + 2, y + 0, er, eg, eb, 4, 32);
                Spread(errR, errG, errB, width, height, x - 2, y + 1, er, eg, eb, 2, 32);
                Spread(errR, errG, errB, width, height, x - 1, y + 1, er, eg, eb, 4, 32);
                Spread(errR, errG, errB, width, height, x + 0, y + 1, er, eg, eb, 8, 32);
                Spread(errR, errG, errB, width, height, x + 1, y + 1, er, eg, eb, 4, 32);
                Spread(errR, errG, errB, width, height, x + 2, y + 1, er, eg, eb, 2, 32);
                break;

            default:
                // No diffusion
                break;
        }
    }

    private static void Spread(
        float[] errR, float[] errG, float[] errB,
        int width, int height,
        int x, int y,
        float er, float eg, float eb,
        int num, int den)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        int idx = y * width + x;
        errR[idx] += er * num / den;
        errG[idx] += eg * num / den;
        errB[idx] += eb * num / den;
    }

    private static float Clamp(float v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // -----------------------------------------------------------------------
    // Median-cut helpers
    // -----------------------------------------------------------------------

    private static int FindBoxToSplit(List<ColorEntry> colors, List<Box> boxes)
    {
        int best = -1;
        int bestRange = -1;
        for (int i = 0; i < boxes.Count; i++)
        {
            Box box = boxes[i];
            if (box.End - box.Start <= 1) continue;
            GetBoxRange(colors, box, out int rr, out int rg, out int rb);
            int range = Math.Max(rr, Math.Max(rg, rb));
            if (range > bestRange)
            {
                bestRange = range;
                best = i;
            }
        }
        return best;
    }

    private static void GetBoxRange(List<ColorEntry> colors, Box box,
        out int rR, out int rG, out int rB)
    {
        int minR = 255, minG = 255, minB = 255;
        int maxR = 0, maxG = 0, maxB = 0;
        for (int i = box.Start; i < box.End; i++)
        {
            var c = colors[i];
            if (c.R < minR) minR = c.R;
            if (c.R > maxR) maxR = c.R;
            if (c.G < minG) minG = c.G;
            if (c.G > maxG) maxG = c.G;
            if (c.B < minB) minB = c.B;
            if (c.B > maxB) maxB = c.B;
        }
        rR = maxR - minR;
        rG = maxG - minG;
        rB = maxB - minB;
    }

    private static void SplitBox(List<ColorEntry> colors, Box box, out Box b1, out Box b2)
    {
        GetBoxRange(colors, box, out int rR, out int rG, out int rB);

        int maxRange = Math.Max(rR, Math.Max(rG, rB));
        if (maxRange == rR)
            colors.Sort(box.Start, box.End - box.Start, ColorComparer.ByR);
        else if (maxRange == rG)
            colors.Sort(box.Start, box.End - box.Start, ColorComparer.ByG);
        else
            colors.Sort(box.Start, box.End - box.Start, ColorComparer.ByB);

        // Split at median weighted by pixel count
        long total = 0;
        for (int i = box.Start; i < box.End; i++) total += colors[i].Count;
        long half = total / 2;
        long running = 0;
        int mid = box.Start;
        for (int i = box.Start; i < box.End - 1; i++)
        {
            running += colors[i].Count;
            if (running >= half) { mid = i + 1; break; }
            mid = i + 1;
        }

        b1 = new Box { Start = box.Start, End = mid };
        b2 = new Box { Start = mid, End = box.End };
    }

    private static void GetBoxRepresentative(List<ColorEntry> colors, Box box,
        out byte r, out byte g, out byte b)
    {
        long sumR = 0, sumG = 0, sumB = 0, sumCount = 0;
        for (int i = box.Start; i < box.End; i++)
        {
            var c = colors[i];
            sumR += (long)c.R * c.Count;
            sumG += (long)c.G * c.Count;
            sumB += (long)c.B * c.Count;
            sumCount += c.Count;
        }
        if (sumCount == 0)
        {
            r = g = b = 0;
            return;
        }
        r = (byte)(sumR / sumCount);
        g = (byte)(sumG / sumCount);
        b = (byte)(sumB / sumCount);
    }

    // -----------------------------------------------------------------------
    // Sort comparisons
    // -----------------------------------------------------------------------

    private static readonly Comparison<ColorEntry> Comparison_R =
        (a, b) => a.R.CompareTo(b.R);

    private static class ColorComparer
    {
        public static readonly IComparer<ColorEntry> ByR = Comparer<ColorEntry>.Create((a, b) => a.R.CompareTo(b.R));
        public static readonly IComparer<ColorEntry> ByG = Comparer<ColorEntry>.Create((a, b) => a.G.CompareTo(b.G));
        public static readonly IComparer<ColorEntry> ByB = Comparer<ColorEntry>.Create((a, b) => a.B.CompareTo(b.B));
    }

    // -----------------------------------------------------------------------
    // Helper types
    // -----------------------------------------------------------------------

    private struct ColorEntry
    {
        public byte R, G, B;
        public int Count;
    }

    private struct Box
    {
        public int Start, End;
    }
}
