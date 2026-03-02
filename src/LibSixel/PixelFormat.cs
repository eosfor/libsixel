namespace LibSixel;

/// <summary>
/// Describes the memory layout of pixel data passed to the encoder.
/// </summary>
public enum PixelFormat
{
    /// <summary>3 bytes per pixel: Red, Green, Blue.</summary>
    RGB888 = 1,

    /// <summary>4 bytes per pixel: Red, Green, Blue, Alpha.</summary>
    RGBA8888 = 2,

    /// <summary>3 bytes per pixel: Blue, Green, Red.</summary>
    BGR888 = 5,

    /// <summary>4 bytes per pixel: Blue, Green, Red, Alpha.</summary>
    BGRA8888 = 6,

    /// <summary>4 bytes per pixel: Alpha, Red, Green, Blue.</summary>
    ARGB8888 = 16,

    /// <summary>1 byte per pixel — palette index.</summary>
    PAL8 = 96,
}
