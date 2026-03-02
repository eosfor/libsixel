using LibSixel;
using LibSixel.Internal;
using Xunit;

namespace LibSixel.Tests;

public class SixelDecoderTests
{
    // -----------------------------------------------------------------------
    // Helper: build a minimal valid SIXEL string for a solid-colour 4×6 image
    // -----------------------------------------------------------------------
    private static string MakeMinimalSixel(int w, int h, byte r, byte g, byte b)
    {
        // Encode a solid-colour image using the encoder and return the string.
        byte[] pixels = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++)
        {
            pixels[i * 3 + 0] = r;
            pixels[i * 3 + 1] = g;
            pixels[i * 3 + 2] = b;
        }
        using var dither = SixelDither.CreateFromImage(pixels, w, h, PixelFormat.RGB888, 2);
        return SixelEncoder.EncodeToString(pixels, w, h, PixelFormat.RGB888, dither);
    }

    // -----------------------------------------------------------------------
    // 1. Decode a minimal valid SIXEL string
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_MinimalSixel_ReturnsNonEmpty()
    {
        string sixel = MakeMinimalSixel(4, 6, 200, 100, 50);
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        Assert.True(result.Width > 0, "Expected non-zero width.");
        Assert.True(result.Height > 0, "Expected non-zero height.");
        Assert.NotEmpty(result.Pixels);
    }

    // -----------------------------------------------------------------------
    // 2. Colour introduction (#Pc;2;r;g;b) is decoded correctly
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_ColorIntro_IsDecoded()
    {
        // Manually craft a SIXEL with a single red colour introduction
        // ESC P 0;1;0q "1;1;1;6 #0;2;100;0;0 #0 ~ - ESC \
        string sixel = "\x1bP0;1;0q\"1;1;1;6#0;2;100;0;0#0~-\x1b\\";
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        Assert.True(result.NumColors >= 1);
        // Colour 0 should be close to red (255, 0, 0)
        Assert.InRange(result.Palette[0], 240, 255);  // R
        Assert.InRange(result.Palette[1], 0, 15);     // G
        Assert.InRange(result.Palette[2], 0, 15);     // B
    }

    // -----------------------------------------------------------------------
    // 3. Repeat (!n c) expands correctly
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_Repeat_ExpandsCorrectly()
    {
        // Build a 10×6 image with a repeat of 10
        // ESC P 0;1;0q "1;1;10;6 #0;2;0;100;0 #0 !10~ - ESC \
        string sixel = "\x1bP0;1;0q\"1;1;10;6#0;2;0;100;0#0!10~-\x1b\\";
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        Assert.Equal(10, result.Width);
        Assert.Equal(6, result.Height);
        Assert.Equal(60, result.Pixels.Length);
    }

    // -----------------------------------------------------------------------
    // 4. New line (-) advances Y by 6
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_NewLine_AdvancesY()
    {
        // Two bands: first band colour 0, second band colour 1
        // Height should be at least 12
        string sixel = "\x1bP0;1;0q"
                     + "\"1;1;2;12"
                     + "#0;2;100;0;0#0~~-"   // band 1 (y=0..5): red
                     + "#1;2;0;0;100#1~~-"   // band 2 (y=6..11): blue
                     + "\x1b\\";
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        Assert.True(result.Height >= 12, $"Expected height >= 12, got {result.Height}.");
    }

    // -----------------------------------------------------------------------
    // 5. Carriage return ($) resets X to 0
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_CarriageReturn_ResetsX()
    {
        // Two colours in the same band using $ to return to column 0
        // #0 draws one column, $ resets, #1 draws over the same column
        // Both colours are in the same band – width should still be 1
        string sixel = "\x1bP0;1;0q"
                     + "\"1;1;1;6"
                     + "#0;2;100;0;0#0~"     // colour 0, column 0
                     + "$"                    // CR
                     + "#1;2;0;0;100#1~"     // colour 1, column 0 (overwrites)
                     + "-\x1b\\";
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        Assert.Equal(1, result.Width);
        Assert.Equal(6, result.Height);
    }

    // -----------------------------------------------------------------------
    // 6. ToRgba32 returns 4 bytes per pixel with alpha = 255
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_ToRgba32_Returns4BytesPerPixel()
    {
        string sixel = MakeMinimalSixel(4, 6, 128, 64, 32);
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        byte[] rgba = result.ToRgba32();
        Assert.Equal(result.Width * result.Height * 4, rgba.Length);

        // All alpha values should be 255
        for (int i = 3; i < rgba.Length; i += 4)
            Assert.Equal(255, rgba[i]);
    }

    // -----------------------------------------------------------------------
    // 7. ToRgb24 returns 3 bytes per pixel
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_ToRgb24_Returns3BytesPerPixel()
    {
        string sixel = MakeMinimalSixel(4, 6, 0, 255, 0);
        SixelDecodeResult result = SixelDecoder.Decode(sixel);

        byte[] rgb = result.ToRgb24();
        Assert.Equal(result.Width * result.Height * 3, rgb.Length);
    }

    // -----------------------------------------------------------------------
    // 8. Decode empty data returns empty result
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_EmptyData_ReturnsEmptyResult()
    {
        SixelDecodeResult result = SixelDecoder.Decode(Array.Empty<byte>());
        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
        Assert.Empty(result.Pixels);
    }

    // -----------------------------------------------------------------------
    // 9. Decode byte overload works the same as string overload
    // -----------------------------------------------------------------------
    [Fact]
    public void Decode_ByteOverload_MatchesStringOverload()
    {
        string sixel = MakeMinimalSixel(4, 6, 50, 100, 150);
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(sixel);

        SixelDecodeResult fromString = SixelDecoder.Decode(sixel);
        SixelDecodeResult fromBytes = SixelDecoder.Decode(bytes);

        Assert.Equal(fromString.Width, fromBytes.Width);
        Assert.Equal(fromString.Height, fromBytes.Height);
        Assert.Equal(fromString.NumColors, fromBytes.NumColors);
    }
}
