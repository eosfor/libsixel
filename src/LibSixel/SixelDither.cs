using LibSixel.Internal;

namespace LibSixel;

/// <summary>
/// Holds palette and quantization settings used when encoding pixel data to SIXEL.
/// </summary>
public sealed class SixelDither : IDisposable
{
    private SixelDither() { }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    /// <summary>
    /// RGB palette as a flat byte array of length <see cref="NumColors"/> * 3.
    /// Each triplet is (R, G, B) in the range 0–255.
    /// </summary>
    public byte[] Palette { get; private set; } = Array.Empty<byte>();

    /// <summary>Number of colours in <see cref="Palette"/>.</summary>
    public int NumColors { get; private set; }

    /// <summary>Pixel format of the source image.</summary>
    public PixelFormat PixelFormat { get; set; } = PixelFormat.RGB888;

    /// <summary>Error-diffusion method to use (see <see cref="SixelConstants.DiffuseNone"/> etc.).</summary>
    public int MethodForDiffuse { get; set; } = SixelConstants.DiffuseAuto;

    /// <summary>Method used to choose which colour box to split next during quantization.</summary>
    public int MethodForLargest { get; set; } = SixelConstants.LargeAuto;

    /// <summary>Method used to pick the representative colour for each box.</summary>
    public int MethodForRep { get; set; } = SixelConstants.RepAuto;

    /// <summary>Quality mode for quantization.</summary>
    public int QualityMode { get; set; } = SixelConstants.QualityAuto;

    /// <summary>When <see langword="true"/>, skip DCS header / footer.</summary>
    public bool BodyOnly { get; set; }

    /// <summary>When <see langword="true"/>, apply palette-optimization passes.</summary>
    public bool Optimized { get; set; }

    /// <summary>When <see langword="true"/>, remove unused palette entries before encoding.</summary>
    public bool OptimizePalette { get; set; }

    /// <summary>Complexion score (1 = normal).  Higher values bias the colour distance metric.</summary>
    public int Complexion { get; set; } = 1;

    // -----------------------------------------------------------------------
    // Built-in palette factories
    // -----------------------------------------------------------------------

    /// <summary>Creates a 2-colour monochrome dither context with a black background.</summary>
    public static SixelDither CreateMonoDark()
    {
        var d = new SixelDither();
        d.Palette = new byte[] { 0, 0, 0, 255, 255, 255 };
        d.NumColors = 2;
        return d;
    }

    /// <summary>Creates a 2-colour monochrome dither context with a white background.</summary>
    public static SixelDither CreateMonoLight()
    {
        var d = new SixelDither();
        d.Palette = new byte[] { 255, 255, 255, 0, 0, 0 };
        d.NumColors = 2;
        return d;
    }

    /// <summary>Creates a dither context using the standard xterm 16-colour palette.</summary>
    public static SixelDither CreateXterm16()
    {
        var d = new SixelDither();
        d.Palette = BuildXterm256Palette(16);
        d.NumColors = 16;
        return d;
    }

    /// <summary>Creates a dither context using the standard xterm 256-colour palette.</summary>
    public static SixelDither CreateXterm256()
    {
        var d = new SixelDither();
        d.Palette = BuildXterm256Palette(256);
        d.NumColors = 256;
        return d;
    }

    /// <summary>
    /// Creates a grayscale dither context with <c>2^<paramref name="bits"/></c> levels.
    /// </summary>
    /// <param name="bits">Number of bits (1–8).  Results in 2, 4, 8, …, 256 grey levels.</param>
    public static SixelDither CreateGrayscale(int bits)
    {
        if (bits < 1 || bits > 8)
            throw new ArgumentOutOfRangeException(nameof(bits), "Must be between 1 and 8.");

        int levels = 1 << bits;
        var palette = new byte[levels * 3];
        for (int i = 0; i < levels; i++)
        {
            byte v = (byte)(i * 255 / (levels - 1));
            palette[i * 3 + 0] = v;
            palette[i * 3 + 1] = v;
            palette[i * 3 + 2] = v;
        }

        var d = new SixelDither();
        d.Palette = palette;
        d.NumColors = levels;
        return d;
    }

    /// <summary>
    /// Quantizes the colours in <paramref name="pixels"/> and returns a dither context
    /// whose palette has at most <paramref name="ncolors"/> entries.
    /// </summary>
    /// <param name="pixels">Raw pixel bytes in the given <paramref name="format"/>.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="format">Pixel memory layout.</param>
    /// <param name="ncolors">Desired number of palette colours (2–256).</param>
    /// <param name="qualityMode">Quantization quality (see <see cref="SixelConstants.QualityAuto"/> etc.).</param>
    public static SixelDither CreateFromImage(
        ReadOnlySpan<byte> pixels,
        int width, int height,
        PixelFormat format,
        int ncolors,
        int qualityMode = SixelConstants.QualityAuto)
    {
        if (ncolors < SixelConstants.PaletteMin || ncolors > SixelConstants.PaletteMax)
            throw new ArgumentOutOfRangeException(nameof(ncolors));

        // Convert to RGB24 first
        byte[] rgb = PixelConverter.ToRgb24(pixels, width, height, format);

        // Quantize
        ColorQuantizer.Quantize(rgb, width, height, ncolors, out byte[] palette);

        var d = new SixelDither();
        d.Palette = palette;
        d.NumColors = palette.Length / 3;
        d.PixelFormat = format;
        d.QualityMode = qualityMode;
        return d;
    }

    // -----------------------------------------------------------------------
    // Palette application
    // -----------------------------------------------------------------------

    /// <summary>
    /// Converts <paramref name="pixels"/> to a palette-indexed byte array.
    /// </summary>
    internal byte[] ApplyPalette(ReadOnlySpan<byte> pixels, int width, int height)
    {
        byte[] rgb = PixelConverter.ToRgb24(pixels, width, height, PixelFormat);
        int diffuse = MethodForDiffuse == SixelConstants.DiffuseAuto
            ? SixelConstants.DiffuseFloydSteinberg
            : MethodForDiffuse;
        return ColorQuantizer.ApplyPalette(rgb, width, height, Palette, NumColors, diffuse);
    }

    // -----------------------------------------------------------------------
    // xterm-256 palette builder
    // -----------------------------------------------------------------------

    internal static byte[] BuildXterm256Palette(int count)
    {
        // Full 256-entry xterm palette
        var full = new byte[256 * 3];

        // First 16: standard colours (matching xterm defaults)
        ReadOnlySpan<uint> std16 = new uint[]
        {
            XRgb(0,0,0),     XRgb(128,0,0),   XRgb(0,128,0),   XRgb(128,128,0),
            XRgb(0,0,128),   XRgb(128,0,128), XRgb(0,128,128), XRgb(192,192,192),
            XRgb(128,128,128),XRgb(255,0,0),  XRgb(0,255,0),   XRgb(255,255,0),
            XRgb(0,0,255),   XRgb(255,0,255), XRgb(0,255,255), XRgb(255,255,255),
        };
        for (int i = 0; i < 16; i++)
        {
            full[i * 3 + 0] = (byte)(std16[i] >> 16);
            full[i * 3 + 1] = (byte)(std16[i] >> 8);
            full[i * 3 + 2] = (byte)(std16[i]);
        }

        // 16-231: 6×6×6 colour cube
        for (int i = 0; i < 216; i++)
        {
            int b = i % 6;
            int g = (i / 6) % 6;
            int r = (i / 36) % 6;
            full[(16 + i) * 3 + 0] = (byte)(r == 0 ? 0 : 55 + r * 40);
            full[(16 + i) * 3 + 1] = (byte)(g == 0 ? 0 : 55 + g * 40);
            full[(16 + i) * 3 + 2] = (byte)(b == 0 ? 0 : 55 + b * 40);
        }

        // 232-255: grayscale ramp
        for (int i = 0; i < 24; i++)
        {
            byte v = (byte)(8 + i * 10);
            full[(232 + i) * 3 + 0] = v;
            full[(232 + i) * 3 + 1] = v;
            full[(232 + i) * 3 + 2] = v;
        }

        if (count >= 256) return full;

        var result = new byte[count * 3];
        Array.Copy(full, result, count * 3);
        return result;
    }

    private static uint XRgb(byte r, byte g, byte b) => ((uint)r << 16) | ((uint)g << 8) | b;

    /// <inheritdoc/>
    public void Dispose() { }
}
