using SkiaSharp;
using Svg.Skia;
using System.Runtime.InteropServices;

namespace LibSixel.PowerShell.Internal;

internal readonly record struct DecodedImage(int Width, int Height, byte[] Pixels);

internal static class ImageDecoder
{
    public static DecodedImage DecodeFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => DecodeRaster(path),
            ".jpg" => DecodeRaster(path),
            ".jpeg" => DecodeRaster(path),
            ".svg" => DecodeSvg(path),
            _ => throw new NotSupportedException(
                $"Unsupported image extension '{extension}'. Supported: .png, .jpg, .jpeg, .svg."),
        };
    }

    public static DecodedImage DecodeSvgContent(string svgContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svgContent);

        string normalizedSvgContent = NormalizeSvgDeclaration(svgContent);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(normalizedSvgContent));
        return DecodeSvg(stream);
    }

    public static DecodedImage Resize(DecodedImage image, int? width, int? height)
    {
        if (width is null && height is null)
        {
            return image;
        }

        int targetWidth;
        int targetHeight;

        if (width is { } requestedWidth && height is { } requestedHeight)
        {
            targetWidth = requestedWidth;
            targetHeight = requestedHeight;
        }
        else if (width is { } computedWidth)
        {
            targetWidth = computedWidth;
            targetHeight = Math.Max(1, (int)Math.Round(image.Height * (targetWidth / (double)image.Width)));
        }
        else
        {
            targetHeight = height!.Value;
            targetWidth = Math.Max(1, (int)Math.Round(image.Width * (targetHeight / (double)image.Height)));
        }

        var sourceInfo = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var sourceBitmap = new SKBitmap(sourceInfo);
        Marshal.Copy(image.Pixels, 0, sourceBitmap.GetPixels(), image.Pixels.Length);

        var targetInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var targetBitmap = new SKBitmap(targetInfo);
        using var canvas = new SKCanvas(targetBitmap);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(sourceBitmap, new SKRect(0, 0, targetWidth, targetHeight));
        canvas.Flush();

        byte[] resizedPixels = new byte[targetWidth * targetHeight * 4];
        Marshal.Copy(targetBitmap.GetPixels(), resizedPixels, 0, resizedPixels.Length);
        return new DecodedImage(targetWidth, targetHeight, resizedPixels);
    }

    private static DecodedImage DecodeRaster(string path)
    {
        byte[] encodedBytes = File.ReadAllBytes(path);
        using SKData data = SKData.CreateCopy(encodedBytes);
        using SKImage image = SKImage.FromEncodedData(data)
            ?? throw new InvalidDataException($"Failed to decode raster image: {path}");

        return ReadRgba(image);
    }

    private static DecodedImage DecodeSvg(string path)
    {
        var svg = new SKSvg();
        SKPicture? picture = svg.Load(path);
        if (picture is null)
            throw new InvalidDataException($"Failed to decode SVG image: {path}");

        return RenderSvgPicture(picture);
    }

    private static DecodedImage DecodeSvg(Stream stream)
    {
        var svg = new SKSvg();
        SKPicture? picture = svg.Load(stream);
        if (picture is null)
            throw new InvalidDataException("Failed to decode SVG image content.");

        return RenderSvgPicture(picture);
    }

    private static DecodedImage RenderSvgPicture(SKPicture picture)
    {
        SKRect bounds = picture.CullRect;
        if (!float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height))
            throw new InvalidDataException("SVG bounds are invalid.");

        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        int height = Math.Max(1, (int)Math.Ceiling(bounds.Height));

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKSurface surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException("Failed to create Skia rendering surface.");

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        float scaleX = width / Math.Max(bounds.Width, 1f);
        float scaleY = height / Math.Max(bounds.Height, 1f);
        canvas.Scale(scaleX, scaleY);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        return ReadRgba(image);
    }

    private static DecodedImage ReadRgba(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);

        bool ok = image.ReadPixels(info, bitmap.GetPixels(), info.RowBytes, 0, 0);
        if (!ok)
            throw new InvalidDataException("Failed to convert image into RGBA pixel buffer.");

        int sourceRowBytes = bitmap.RowBytes;
        int packedRowBytes = checked(info.Width * 4);
        byte[] source = new byte[checked(sourceRowBytes * info.Height)];
        Marshal.Copy(bitmap.GetPixels(), source, 0, source.Length);

        if (sourceRowBytes == packedRowBytes)
            return new DecodedImage(info.Width, info.Height, FlattenTransparencyToWhite(source));

        byte[] packed = new byte[checked(packedRowBytes * info.Height)];
        for (int y = 0; y < info.Height; y++)
        {
            Buffer.BlockCopy(source, y * sourceRowBytes, packed, y * packedRowBytes, packedRowBytes);
        }

        return new DecodedImage(info.Width, info.Height, FlattenTransparencyToWhite(packed));
    }

    private static string NormalizeSvgDeclaration(string svgContent)
    {
        if (!svgContent.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            return svgContent;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            svgContent,
            "encoding\\s*=\\s*\"[^\"]+\"",
            "encoding=\"utf-8\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
    }

    private static byte[] FlattenTransparencyToWhite(byte[] rgbaPixels)
    {
        for (int i = 0; i < rgbaPixels.Length; i += 4)
        {
            byte alpha = rgbaPixels[i + 3];
            if (alpha == byte.MaxValue)
            {
                continue;
            }

            if (alpha == 0)
            {
                rgbaPixels[i] = byte.MaxValue;
                rgbaPixels[i + 1] = byte.MaxValue;
                rgbaPixels[i + 2] = byte.MaxValue;
                rgbaPixels[i + 3] = byte.MaxValue;
                continue;
            }

            float a = alpha / 255f;
            rgbaPixels[i] = (byte)Math.Round((rgbaPixels[i] * a) + (255f * (1f - a)));
            rgbaPixels[i + 1] = (byte)Math.Round((rgbaPixels[i + 1] * a) + (255f * (1f - a)));
            rgbaPixels[i + 2] = (byte)Math.Round((rgbaPixels[i + 2] * a) + (255f * (1f - a)));
            rgbaPixels[i + 3] = byte.MaxValue;
        }

        return rgbaPixels;
    }
}
