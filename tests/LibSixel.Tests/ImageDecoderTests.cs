using LibSixel.PowerShell.Internal;
using Xunit;

namespace LibSixel.Tests;

public sealed class ImageDecoderTests
{
    [Fact]
    public void DecodeSvgContent_ReturnsPixelBuffer()
    {
        const string svg = """
            <?xml version="1.0" encoding="utf-16"?>
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
              <rect x="0" y="0" width="16" height="16" fill="#ffffff" />
              <circle cx="8" cy="8" r="5" fill="#ff0000" />
            </svg>
            """;

        var image = ImageDecoder.DecodeSvgContent(svg);

        Assert.Equal(16, image.Width);
        Assert.Equal(16, image.Height);
        Assert.Equal(16 * 16 * 4, image.Pixels.Length);
    }

    [Fact]
    public void Resize_WithWidthAndHeight_ReturnsRequestedSize()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="20">
              <rect x="0" y="0" width="10" height="20" fill="#00ff00" />
            </svg>
            """;

        var image = ImageDecoder.DecodeSvgContent(svg);
        var resized = ImageDecoder.Resize(image, 30, 40);

        Assert.Equal(30, resized.Width);
        Assert.Equal(40, resized.Height);
        Assert.Equal(30 * 40 * 4, resized.Pixels.Length);
    }

    [Fact]
    public void Resize_WithOnlyWidth_PreservesAspectRatio()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="20">
              <rect x="0" y="0" width="10" height="20" fill="#0000ff" />
            </svg>
            """;

        var image = ImageDecoder.DecodeSvgContent(svg);
        var resized = ImageDecoder.Resize(image, 30, null);

        Assert.Equal(30, resized.Width);
        Assert.Equal(60, resized.Height);
    }

    [Fact]
    public void DecodeSvgContent_FlattensTransparentPixelsToWhite()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="2" height="1">
              <rect x="1" y="0" width="1" height="1" fill="#ff0000" />
            </svg>
            """;

        var image = ImageDecoder.DecodeSvgContent(svg);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(byte.MaxValue, image.Pixels[0]);
        Assert.Equal(byte.MaxValue, image.Pixels[1]);
        Assert.Equal(byte.MaxValue, image.Pixels[2]);
        Assert.Equal(byte.MaxValue, image.Pixels[3]);
    }
}
