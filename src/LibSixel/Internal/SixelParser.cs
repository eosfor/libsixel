using System.Text;

namespace LibSixel.Internal;

// ---------------------------------------------------------------------------
// State machine states (mirroring fromsixel.c)
// ---------------------------------------------------------------------------
internal enum ParseState
{
    Ground,    // Waiting for ESC or DCS
    Esc,       // Saw ESC
    Dcs,       // Inside DCS parameters, waiting for 'q'
    Decsixel,  // Parsing sixel body
    Decgra,    // Parsing raster attributes after '"'
    Decgri,    // Parsing repeat count after '!'
    Decgci,    // Parsing colour index / definition after '#'
}

/// <summary>
/// Decoded image produced by <see cref="SixelParser"/>.
/// </summary>
public sealed class SixelDecodeResult
{
    /// <summary>Palette-indexed pixel data, one byte per pixel.</summary>
    public required byte[] Pixels { get; init; }

    /// <summary>Flat RGB24 palette (length = <see cref="NumColors"/> × 3).</summary>
    public required byte[] Palette { get; init; }

    /// <summary>Number of colours defined in <see cref="Palette"/>.</summary>
    public int NumColors { get; init; }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Converts the indexed image to RGBA32 (R, G, B, A per pixel, A = 255).</summary>
    public byte[] ToRgba32()
    {
        var rgba = new byte[Width * Height * 4];
        for (int i = 0; i < Width * Height; i++)
        {
            int ci = Pixels[i];
            rgba[i * 4 + 0] = ci < NumColors ? Palette[ci * 3 + 0] : (byte)0;
            rgba[i * 4 + 1] = ci < NumColors ? Palette[ci * 3 + 1] : (byte)0;
            rgba[i * 4 + 2] = ci < NumColors ? Palette[ci * 3 + 2] : (byte)0;
            rgba[i * 4 + 3] = 255;
        }
        return rgba;
    }

    /// <summary>Converts the indexed image to RGB24 (R, G, B per pixel).</summary>
    public byte[] ToRgb24()
    {
        var rgb = new byte[Width * Height * 3];
        for (int i = 0; i < Width * Height; i++)
        {
            int ci = Pixels[i];
            rgb[i * 3 + 0] = ci < NumColors ? Palette[ci * 3 + 0] : (byte)0;
            rgb[i * 3 + 1] = ci < NumColors ? Palette[ci * 3 + 1] : (byte)0;
            rgb[i * 3 + 2] = ci < NumColors ? Palette[ci * 3 + 2] : (byte)0;
        }
        return rgb;
    }
}

/// <summary>
/// SIXEL stream parser, ported from <c>fromsixel.c</c> in the original libsixel C library.
/// </summary>
internal static class SixelParser
{
    // Default 16-colour palette from fromsixel.c
    // SIXEL_XRGB(r,g,b) = ((r*255+50)/100 << 16) | ((g*255+50)/100 << 8) | ((b*255+50)/100)
    private static readonly byte[] DefaultPalette16;

    static SixelParser()
    {
        var src = new (int r, int g, int b)[]
        {
            (0,  0,  0),   //  0 Black
            (20, 20, 80),  //  1 Blue
            (80, 13, 13),  //  2 Red
            (20, 80, 20),  //  3 Green
            (80, 20, 80),  //  4 Magenta
            (20, 80, 80),  //  5 Cyan
            (80, 80, 20),  //  6 Yellow
            (53, 53, 53),  //  7 Gray 50%
            (26, 26, 26),  //  8 Gray 25%
            (33, 33, 60),  //  9 Blue*
            (60, 26, 26),  // 10 Red*
            (33, 60, 33),  // 11 Green*
            (60, 33, 60),  // 12 Magenta*
            (33, 60, 60),  // 13 Cyan*
            (60, 60, 33),  // 14 Yellow*
            (80, 80, 80),  // 15 Gray 75%
        };

        DefaultPalette16 = new byte[16 * 3];
        for (int i = 0; i < src.Length; i++)
        {
            DefaultPalette16[i * 3 + 0] = (byte)((src[i].r * 255 + 50) / 100);
            DefaultPalette16[i * 3 + 1] = (byte)((src[i].g * 255 + 50) / 100);
            DefaultPalette16[i * 3 + 2] = (byte)((src[i].b * 255 + 50) / 100);
        }
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    /// <summary>Parses a SIXEL byte stream and returns the decoded image.</summary>
    public static SixelDecodeResult Decode(ReadOnlySpan<byte> data)
    {
        var ctx = new DecodeContext();
        ctx.InitPalette();

        var state = ParseState.Ground;
        int i = 0;

        while (i < data.Length)
        {
            byte b = data[i++];
            state = ProcessByte(b, ref ctx, state, data, ref i);
            if (ctx.Done) break;
        }

        // Finalise: trim canvas to actual content
        int finalWidth = ctx.Width;
        int finalHeight = ctx.Height;

        if (finalWidth == 0 || finalHeight == 0)
            return new SixelDecodeResult
            {
                Pixels = Array.Empty<byte>(),
                Palette = Array.Empty<byte>(),
                NumColors = 0,
                Width = 0,
                Height = 0,
            };

        // Copy only the needed portion of pixels
        byte[] pixels = TrimPixels(ctx, finalWidth, finalHeight);
        byte[] palette = BuildOutputPalette(ctx);

        return new SixelDecodeResult
        {
            Pixels = pixels,
            Palette = palette,
            NumColors = ctx.NumColors,
            Width = finalWidth,
            Height = finalHeight,
        };
    }

    // -----------------------------------------------------------------------
    // State machine
    // -----------------------------------------------------------------------

    private static ParseState ProcessByte(
        byte b,
        ref DecodeContext ctx,
        ParseState state,
        ReadOnlySpan<byte> data,
        ref int pos)
    {
        switch (state)
        {
            case ParseState.Ground:
                if (b == 0x1B) return ParseState.Esc;
                if (b == 'q') { ctx.EnterSixelBody(); return ParseState.Decsixel; }
                return ParseState.Ground;

            case ParseState.Esc:
                if (b == 'P') return ParseState.Dcs;
                if (b == '\\') { ctx.Done = true; return ParseState.Ground; }
                return ParseState.Ground;

            case ParseState.Dcs:
                // Consume DCS parameters until 'q'
                if (b == 'q') { ctx.EnterSixelBody(); return ParseState.Decsixel; }
                if (b >= '0' && b <= '9') { ctx.AddParamDigit(b); return ParseState.Dcs; }
                if (b == ';') { ctx.NextParam(); return ParseState.Dcs; }
                return ParseState.Dcs;

            case ParseState.Decsixel:
                return ProcessSixelByte(b, ref ctx);

            case ParseState.Decgra:
                return ProcessGraByte(b, ref ctx);

            case ParseState.Decgri:
                return ProcessGriByte(b, ref ctx);

            case ParseState.Decgci:
                return ProcessGciByte(b, ref ctx);

            default:
                return ParseState.Ground;
        }
    }

    private static ParseState ProcessSixelByte(byte b, ref DecodeContext ctx)
    {
        if (b >= '?' && b <= '~')
        {
            // Sixel data byte
            int bits = b - '?';
            int repeat = ctx.RepeatCount > 0 ? ctx.RepeatCount : 1;
            ctx.PaintSixel(bits, repeat);
            ctx.RepeatCount = 0;
            return ParseState.Decsixel;
        }

        switch (b)
        {
            case (byte)'"':
                ctx.GraParams[0] = ctx.GraParams[1] = ctx.GraParams[2] = ctx.GraParams[3] = 0;
                ctx.GraParamIdx = 0;
                return ParseState.Decgra;

            case (byte)'!':
                ctx.RepeatCount = 0;
                return ParseState.Decgri;

            case (byte)'#':
                ctx.GciParams[0] = ctx.GciParams[1] = ctx.GciParams[2] = ctx.GciParams[3] = ctx.GciParams[4] = 0;
                ctx.GciParamIdx = 0;
                return ParseState.Decgci;

            case (byte)'$':
                // DECGCR – carriage return
                ctx.CarriageReturn();
                return ParseState.Decsixel;

            case (byte)'-':
                // DECGNL – new line
                ctx.NewLine();
                return ParseState.Decsixel;

            case 0x1B:
                // ESC – peek for '\'
                return ParseState.Esc;

            case 0x9C:
                // 8-bit ST
                ctx.Done = true;
                return ParseState.Ground;

            default:
                return ParseState.Decsixel;
        }
    }

    private static ParseState ProcessGraByte(byte b, ref DecodeContext ctx)
    {
        if (b >= '0' && b <= '9') { ctx.GraParams[ctx.GraParamIdx] = ctx.GraParams[ctx.GraParamIdx] * 10 + (b - '0'); return ParseState.Decgra; }
        if (b == ';') { if (ctx.GraParamIdx < 3) ctx.GraParamIdx++; return ParseState.Decgra; }
        // Done with raster attributes – apply them
        ctx.ApplyRasterAttribs();
        return ProcessSixelByte(b, ref ctx);
    }

    private static ParseState ProcessGriByte(byte b, ref DecodeContext ctx)
    {
        if (b >= '0' && b <= '9') { ctx.RepeatCount = ctx.RepeatCount * 10 + (b - '0'); return ParseState.Decgri; }
        return ProcessSixelByte(b, ref ctx);
    }

    private static ParseState ProcessGciByte(byte b, ref DecodeContext ctx)
    {
        if (b >= '0' && b <= '9') { ctx.GciParams[ctx.GciParamIdx] = ctx.GciParams[ctx.GciParamIdx] * 10 + (b - '0'); return ParseState.Decgci; }
        if (b == ';') { if (ctx.GciParamIdx < 4) ctx.GciParamIdx++; return ParseState.Decgci; }
        // Done – apply colour definition
        ctx.ApplyColorIntro();
        return ProcessSixelByte(b, ref ctx);
    }

    // -----------------------------------------------------------------------
    // Output helpers
    // -----------------------------------------------------------------------

    private static byte[] TrimPixels(DecodeContext ctx, int w, int h)
    {
        if (ctx.CanvasWidth == w && ctx.CanvasHeight == h && ctx.Pixels != null)
            return ctx.Pixels;

        var pixels = new byte[w * h];
        if (ctx.Pixels == null) return pixels;

        for (int y = 0; y < h; y++)
        {
            int srcOff = y * ctx.CanvasWidth;
            int dstOff = y * w;
            int copyLen = Math.Min(w, ctx.CanvasWidth);
            Array.Copy(ctx.Pixels, srcOff, pixels, dstOff, copyLen);
        }
        return pixels;
    }

    private static byte[] BuildOutputPalette(DecodeContext ctx)
    {
        var pal = new byte[ctx.NumColors * 3];
        for (int i = 0; i < ctx.NumColors; i++)
        {
            pal[i * 3 + 0] = ctx.PaletteR[i];
            pal[i * 3 + 1] = ctx.PaletteG[i];
            pal[i * 3 + 2] = ctx.PaletteB[i];
        }
        return pal;
    }

    // -----------------------------------------------------------------------
    // Decode context (mutable state during parsing)
    // -----------------------------------------------------------------------

    private struct DecodeContext
    {
        public byte[]? Pixels;
        public int CanvasWidth, CanvasHeight;
        public int Width, Height;      // actual content size
        public int X, Y;               // current drawing position
        public int CurrentColor;
        public int RepeatCount;
        public bool Done;
        public int NumColors;

        // Per-colour palette arrays (up to 256 entries)
        public byte[] PaletteR, PaletteG, PaletteB;
        public bool[] PaletteDefined;

        // Raster attribute parsing
        public int[] GraParams;
        public int GraParamIdx;

        // Colour-intro parsing
        public int[] GciParams;
        public int GciParamIdx;

        // DCS parameter parsing
        private int[] _dcsParams;
        private int _dcsParamIdx;

        public void InitPalette()
        {
            PaletteR = new byte[256];
            PaletteG = new byte[256];
            PaletteB = new byte[256];
            PaletteDefined = new bool[256];
            GraParams = new int[4];
            GciParams = new int[5];
            _dcsParams = new int[4];

            // Load the default 16-colour palette
            for (int i = 0; i < 16; i++)
            {
                PaletteR[i] = DefaultPalette16[i * 3 + 0];
                PaletteG[i] = DefaultPalette16[i * 3 + 1];
                PaletteB[i] = DefaultPalette16[i * 3 + 2];
                PaletteDefined[i] = true;
            }
            NumColors = 0;
        }

        public void AddParamDigit(byte b) => _dcsParams[_dcsParamIdx] = _dcsParams[_dcsParamIdx] * 10 + (b - '0');
        public void NextParam() { if (_dcsParamIdx < 3) _dcsParamIdx++; }

        public void EnterSixelBody()
        {
            // Allocate initial canvas
            EnsureCanvas(100, 6);
        }

        public void PaintSixel(int bits, int repeat)
        {
            if (repeat <= 0) repeat = 1;
            EnsureCanvas(X + repeat, Y + 6);

            for (int dx = 0; dx < repeat; dx++)
            {
                for (int row = 0; row < 6; row++)
                {
                    if ((bits & (1 << row)) != 0)
                    {
                        int px = (Y + row) * CanvasWidth + (X + dx);
                        if (px < Pixels!.Length)
                            Pixels[px] = (byte)CurrentColor;
                    }
                }
                // Track actual content size
                if (X + dx + 1 > Width) Width = X + dx + 1;
            }
            if (Y + 6 > Height) Height = Y + 6;  // will trim later if last band is partial
            X += repeat;
        }

        public void CarriageReturn() => X = 0;

        public void NewLine()
        {
            X = 0;
            Y += 6;
            EnsureCanvas(CanvasWidth, Y + 6);
        }

        public void ApplyRasterAttribs()
        {
            // GraParams: pan, pad, ph, pv
            int pan = GraParams[0];
            int pad = GraParams[1];
            int ph = GraParams[2];
            int pv = GraParams[3];
            if (ph > 0 && pv > 0)
            {
                Width = ph;
                Height = pv;
                EnsureCanvas(ph, pv);
            }
        }

        public void ApplyColorIntro()
        {
            // GciParams: index, type, v1, v2, v3
            int idx = GciParams[0];
            if (idx < 0 || idx >= 256) { CurrentColor = idx < 0 ? 0 : 255; return; }

            CurrentColor = idx;
            if (idx + 1 > NumColors) NumColors = idx + 1;

            int type = GciParams[1];
            if (type == 2)
            {
                // RGB (values 0–100)
                PaletteR[idx] = (byte)(GciParams[2] * 255 / 100);
                PaletteG[idx] = (byte)(GciParams[3] * 255 / 100);
                PaletteB[idx] = (byte)(GciParams[4] * 255 / 100);
                PaletteDefined[idx] = true;
            }
            else if (type == 1)
            {
                // HLS (hue, lightness, saturation)
                float h = GciParams[2] * 360f / 100f;
                float l = GciParams[3] / 100f;
                float s = GciParams[4] / 100f;
                HlsToRgb(h, l, s, out byte r, out byte g, out byte b);
                PaletteR[idx] = r;
                PaletteG[idx] = g;
                PaletteB[idx] = b;
                PaletteDefined[idx] = true;
            }
            // else: just select the colour without redefining it
        }

        private void EnsureCanvas(int requiredW, int requiredH)
        {
            if (Pixels == null)
            {
                CanvasWidth = Math.Max(requiredW, 100);
                CanvasHeight = Math.Max(requiredH, 6);
                Pixels = new byte[CanvasWidth * CanvasHeight];
                return;
            }

            if (requiredW <= CanvasWidth && requiredH <= CanvasHeight) return;

            int newW = Math.Max(requiredW, CanvasWidth * 2);
            int newH = Math.Max(requiredH, CanvasHeight * 2);
            var newPixels = new byte[newW * newH];

            // Copy existing rows
            for (int row = 0; row < CanvasHeight; row++)
                Array.Copy(Pixels, row * CanvasWidth, newPixels, row * newW, CanvasWidth);

            Pixels = newPixels;
            CanvasWidth = newW;
            CanvasHeight = newH;
        }
    }

    // -----------------------------------------------------------------------
    // HLS → RGB conversion
    // -----------------------------------------------------------------------

    private static void HlsToRgb(float h, float l, float s, out byte r, out byte g, out byte b)
    {
        if (s == 0)
        {
            byte v = (byte)(l * 255);
            r = g = b = v;
            return;
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        r = (byte)(HueToRgb(p, q, h / 360f + 1f / 3f) * 255);
        g = (byte)(HueToRgb(p, q, h / 360f) * 255);
        b = (byte)(HueToRgb(p, q, h / 360f - 1f / 3f) * 255);
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }
}
