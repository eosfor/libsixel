namespace LibSixel.Internal;

/// <summary>
/// Converts between different pixel-buffer layouts and RGB24.
/// </summary>
internal static class PixelConverter
{
    /// <summary>
    /// Returns a new byte array containing the image data in packed RGB24 order (R, G, B per pixel).
    /// </summary>
    internal static byte[] ToRgb24(ReadOnlySpan<byte> src, int width, int height, PixelFormat format)
    {
        int nPixels = width * height;
        var dst = new byte[nPixels * 3];

        switch (format)
        {
            case PixelFormat.RGB888:
                if (src.Length < nPixels * 3)
                    throw new ArgumentException("Source buffer too small for RGB888.");
                src.Slice(0, nPixels * 3).CopyTo(dst);
                break;

            case PixelFormat.RGBA8888:
                if (src.Length < nPixels * 4)
                    throw new ArgumentException("Source buffer too small for RGBA8888.");
                for (int i = 0; i < nPixels; i++)
                {
                    dst[i * 3 + 0] = src[i * 4 + 0];
                    dst[i * 3 + 1] = src[i * 4 + 1];
                    dst[i * 3 + 2] = src[i * 4 + 2];
                    // alpha ignored
                }
                break;

            case PixelFormat.BGR888:
                if (src.Length < nPixels * 3)
                    throw new ArgumentException("Source buffer too small for BGR888.");
                for (int i = 0; i < nPixels; i++)
                {
                    dst[i * 3 + 0] = src[i * 3 + 2];
                    dst[i * 3 + 1] = src[i * 3 + 1];
                    dst[i * 3 + 2] = src[i * 3 + 0];
                }
                break;

            case PixelFormat.BGRA8888:
                if (src.Length < nPixels * 4)
                    throw new ArgumentException("Source buffer too small for BGRA8888.");
                for (int i = 0; i < nPixels; i++)
                {
                    dst[i * 3 + 0] = src[i * 4 + 2];
                    dst[i * 3 + 1] = src[i * 4 + 1];
                    dst[i * 3 + 2] = src[i * 4 + 0];
                }
                break;

            case PixelFormat.ARGB8888:
                if (src.Length < nPixels * 4)
                    throw new ArgumentException("Source buffer too small for ARGB8888.");
                for (int i = 0; i < nPixels; i++)
                {
                    dst[i * 3 + 0] = src[i * 4 + 1];
                    dst[i * 3 + 1] = src[i * 4 + 2];
                    dst[i * 3 + 2] = src[i * 4 + 3];
                }
                break;

            case PixelFormat.PAL8:
                throw new ArgumentException("PAL8 cannot be converted without a palette.");

            default:
                throw new ArgumentException($"Unsupported pixel format: {format}.");
        }

        return dst;
    }
}
