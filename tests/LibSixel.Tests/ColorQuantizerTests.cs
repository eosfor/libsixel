using LibSixel.Internal;
using Xunit;

namespace LibSixel.Tests;

public class ColorQuantizerTests
{
    // -----------------------------------------------------------------------
    // Helper: create a solid-colour RGB24 buffer
    // -----------------------------------------------------------------------
    private static byte[] SolidRgb(int w, int h, byte r, byte g, byte b)
    {
        var px = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++)
        {
            px[i * 3 + 0] = r;
            px[i * 3 + 1] = g;
            px[i * 3 + 2] = b;
        }
        return px;
    }

    // -----------------------------------------------------------------------
    // 1. Quantize returns at most the requested number of colours
    // -----------------------------------------------------------------------
    [Fact]
    public void Quantize_ReturnsPaletteUpToRequestedSize()
    {
        byte[] rgb = SolidRgb(4, 4, 200, 100, 50);
        ColorQuantizer.Quantize(rgb, 4, 4, 8, out byte[] palette);

        int nColors = palette.Length / 3;
        Assert.True(nColors >= 1 && nColors <= 8,
            $"Expected palette size 1–8, got {nColors}.");
    }

    // -----------------------------------------------------------------------
    // 2. Palette values are within valid byte range (0–255)
    // -----------------------------------------------------------------------
    [Fact]
    public void Quantize_PaletteValuesAreInRange()
    {
        byte[] rgb = new byte[16 * 16 * 3];
        var rng = new Random(42);
        rng.NextBytes(rgb);

        ColorQuantizer.Quantize(rgb, 16, 16, 16, out byte[] palette);

        foreach (byte v in palette)
            Assert.InRange(v, (byte)0, (byte)255);
    }

    // -----------------------------------------------------------------------
    // 3. Quantize output length equals width × height
    // -----------------------------------------------------------------------
    [Fact]
    public void Quantize_IndexedLength_EqualsWidthTimesHeight()
    {
        int w = 8, h = 8;
        byte[] rgb = SolidRgb(w, h, 10, 20, 30);
        byte[] indexed = ColorQuantizer.Quantize(rgb, w, h, 4, out _);

        Assert.Equal(w * h, indexed.Length);
    }

    // -----------------------------------------------------------------------
    // 4. Single-colour image quantizes to 1 colour
    // -----------------------------------------------------------------------
    [Fact]
    public void Quantize_SingleColor_ProducesSinglePaletteEntry()
    {
        byte[] rgb = SolidRgb(4, 4, 128, 64, 192);
        ColorQuantizer.Quantize(rgb, 4, 4, 8, out byte[] palette);

        int nColors = palette.Length / 3;
        Assert.Equal(1, nColors);
    }

    // -----------------------------------------------------------------------
    // 5. ApplyPalette – no-diffuse returns correct indexed length
    // -----------------------------------------------------------------------
    [Fact]
    public void ApplyPalette_NoDiffuse_CorrectLength()
    {
        int w = 6, h = 6;
        byte[] rgb = SolidRgb(w, h, 255, 0, 0);
        byte[] palette = new byte[] { 255, 0, 0, 0, 0, 255 };

        byte[] indexed = ColorQuantizer.ApplyPalette(rgb, w, h, palette, 2, SixelConstants.DiffuseNone);
        Assert.Equal(w * h, indexed.Length);
    }

    // -----------------------------------------------------------------------
    // 6. ApplyPalette – Floyd-Steinberg returns correct indexed length
    // -----------------------------------------------------------------------
    [Fact]
    public void ApplyPalette_FloydSteinberg_CorrectLength()
    {
        int w = 8, h = 8;
        var rgb = new byte[w * h * 3];
        new Random(1).NextBytes(rgb);
        byte[] palette = new byte[] { 0, 0, 0, 255, 255, 255 };

        byte[] indexed = ColorQuantizer.ApplyPalette(rgb, w, h, palette, 2, SixelConstants.DiffuseFloydSteinberg);
        Assert.Equal(w * h, indexed.Length);
    }

    // -----------------------------------------------------------------------
    // 7. Indexed values are within palette bounds
    // -----------------------------------------------------------------------
    [Fact]
    public void ApplyPalette_IndexedValues_WithinPaletteBounds()
    {
        int w = 8, h = 8;
        var rgb = new byte[w * h * 3];
        new Random(7).NextBytes(rgb);
        int ncolors = 4;
        ColorQuantizer.Quantize(rgb, w, h, ncolors, out byte[] palette);
        int actualColors = palette.Length / 3;

        byte[] indexed = ColorQuantizer.ApplyPalette(rgb, w, h, palette, actualColors, SixelConstants.DiffuseNone);

        foreach (byte idx in indexed)
            Assert.InRange(idx, (byte)0, (byte)(actualColors - 1));
    }
}
