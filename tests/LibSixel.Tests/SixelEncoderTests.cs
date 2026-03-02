using System.Text;
using LibSixel;
using LibSixel.Internal;
using Xunit;

namespace LibSixel.Tests;

public class SixelEncoderTests
{
    // -----------------------------------------------------------------------
    // Helper: build a solid-colour RGB888 image
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
    // 1. Single pixel encodes without exception
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_1x1_SolidRed_DoesNotThrow()
    {
        byte[] pixels = SolidRgb(1, 1, 255, 0, 0);
        using var dither = SixelDither.CreateMonoDark();
        dither.PixelFormat = PixelFormat.RGB888;

        string sixel = SixelEncoder.EncodeToString(pixels, 1, 1, PixelFormat.RGB888, dither);
        Assert.NotEmpty(sixel);
    }

    // -----------------------------------------------------------------------
    // 2. Output starts with DCS introducer ESC P
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_Output_StartsWith_EscP()
    {
        byte[] pixels = SolidRgb(4, 6, 0, 128, 0);
        using var dither = SixelDither.CreateXterm16();

        byte[] bytes = SixelEncoder.EncodeToBytes(pixels, 4, 6, PixelFormat.RGB888, dither);

        Assert.True(bytes.Length >= 2, "Output should have at least 2 bytes.");
        Assert.Equal(0x1B, bytes[0]); // ESC
        Assert.Equal((byte)'P', bytes[1]);
    }

    // -----------------------------------------------------------------------
    // 3. Output ends with ST (ESC \)
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_Output_EndsWith_ST()
    {
        byte[] pixels = SolidRgb(2, 6, 100, 100, 100);
        using var dither = SixelDither.CreateMonoDark();

        byte[] bytes = SixelEncoder.EncodeToBytes(pixels, 2, 6, PixelFormat.RGB888, dither);

        int n = bytes.Length;
        Assert.True(n >= 2);
        Assert.Equal(0x1B, bytes[n - 2]); // ESC
        Assert.Equal((byte)'\\', bytes[n - 1]);
    }

    // -----------------------------------------------------------------------
    // 4. 2×6 two-colour image encodes both colours
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_2x6_TwoColors_ContainsBothColorIntros()
    {
        // Left column = pure red, right column = pure blue
        var pixels = new byte[2 * 6 * 3];
        for (int y = 0; y < 6; y++)
        {
            // Left pixel – red
            pixels[(y * 2 + 0) * 3 + 0] = 255;
            pixels[(y * 2 + 0) * 3 + 1] = 0;
            pixels[(y * 2 + 0) * 3 + 2] = 0;
            // Right pixel – blue
            pixels[(y * 2 + 1) * 3 + 0] = 0;
            pixels[(y * 2 + 1) * 3 + 1] = 0;
            pixels[(y * 2 + 1) * 3 + 2] = 255;
        }

        using var dither = SixelDither.CreateFromImage(pixels, 2, 6, PixelFormat.RGB888, 2);
        string sixel = SixelEncoder.EncodeToString(pixels, 2, 6, PixelFormat.RGB888, dither);

        // Should contain colour intro '#' characters
        Assert.Contains('#', sixel);
    }

    // -----------------------------------------------------------------------
    // 5. Round-trip: encode then decode produces same dimensions
    // -----------------------------------------------------------------------
    [Fact]
    public void RoundTrip_EncodeDecode_SameDimensions()
    {
        int w = 8, h = 6;
        byte[] pixels = SolidRgb(w, h, 200, 100, 50);
        using var dither = SixelDither.CreateFromImage(pixels, w, h, PixelFormat.RGB888, 4);

        byte[] sixelBytes = SixelEncoder.EncodeToBytes(pixels, w, h, PixelFormat.RGB888, dither);
        SixelDecodeResult result = SixelDecoder.Decode(sixelBytes);

        Assert.Equal(w, result.Width);
        Assert.Equal(h, result.Height);
    }

    // -----------------------------------------------------------------------
    // 6. RGBA8888 input is accepted
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_RGBA8888_DoesNotThrow()
    {
        int w = 4, h = 6;
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            pixels[i * 4 + 0] = 128;  // R
            pixels[i * 4 + 1] = 64;   // G
            pixels[i * 4 + 2] = 192;  // B
            pixels[i * 4 + 3] = 255;  // A
        }

        using var dither = SixelDither.CreateFromImage(pixels, w, h, PixelFormat.RGBA8888, 4);
        byte[] sixelBytes = SixelEncoder.EncodeToBytes(pixels, w, h, PixelFormat.RGBA8888, dither);
        Assert.NotEmpty(sixelBytes);
    }

    // -----------------------------------------------------------------------
    // 7. BodyOnly skips DCS header/footer
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_BodyOnly_SkipsDcsHeaderAndFooter()
    {
        byte[] pixels = SolidRgb(2, 6, 0, 0, 255);
        using var dither = SixelDither.CreateMonoDark();
        dither.BodyOnly = true;

        byte[] bytes = SixelEncoder.EncodeToBytes(pixels, 2, 6, PixelFormat.RGB888, dither);

        Assert.False(bytes.Length >= 2 && bytes[0] == 0x1B && bytes[1] == (byte)'P',
            "Body-only output should NOT start with ESC P.");
    }

    // -----------------------------------------------------------------------
    // 8. Negative: zero width throws
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_ZeroWidth_Throws()
    {
        byte[] pixels = new byte[6 * 3];
        using var dither = SixelDither.CreateMonoDark();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SixelEncoder.EncodeToBytes(pixels, 0, 6, PixelFormat.RGB888, dither));
    }

    // -----------------------------------------------------------------------
    // 9. Raster-attribute line is present in output
    // -----------------------------------------------------------------------
    [Fact]
    public void Encode_Output_ContainsRasterAttributes()
    {
        byte[] pixels = SolidRgb(4, 6, 128, 128, 128);
        using var dither = SixelDither.CreateMonoDark();

        string sixel = SixelEncoder.EncodeToString(pixels, 4, 6, PixelFormat.RGB888, dither);

        // Raster attributes start with '"'
        Assert.Contains('"', sixel);
    }
}
