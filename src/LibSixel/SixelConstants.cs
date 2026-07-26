namespace LibSixel;

/// <summary>
/// Constants mirroring the libsixel C library definitions.
/// </summary>
public static class SixelConstants
{
    // Palette size limits
    /// <summary>Minimum palette size.</summary>
    public const int PaletteMin = 2;
    /// <summary>Maximum palette size.</summary>
    public const int PaletteMax = 256;

    // Image dimension limits
    /// <summary>Maximum image width in pixels.</summary>
    public const int WidthLimit = 1_000_000;
    /// <summary>Maximum image height in pixels.</summary>
    public const int HeightLimit = 1_000_000;

    // Status codes
    /// <summary>Operation succeeded.</summary>
    public const int OK = 0x0000;
    /// <summary>False / condition not met (not an error).</summary>
    public const int False = 0x1000;
    /// <summary>Memory allocation failure.</summary>
    public const int BadAllocation = 0x1101;
    /// <summary>Bad argument supplied.</summary>
    public const int BadArgument = 0x1102;
    /// <summary>Bad or malformed input data.</summary>
    public const int BadInput = 0x1103;
    /// <summary>Feature not implemented.</summary>
    public const int NotImplemented = 0x1301;

    // Quantization method for finding the largest dimension (LARGE_*)
    /// <summary>Choose largest dimension automatically.</summary>
    public const int LargeAuto = 0;
    /// <summary>Choose the dimension with the largest variance.</summary>
    public const int LargeNorm = 1;
    /// <summary>Choose the dimension with the largest range.</summary>
    public const int LargeLinf = 2;

    // Color representative selection method (REP_*)
    /// <summary>Choose representative color automatically.</summary>
    public const int RepAuto = 0;
    /// <summary>Use center of the box.</summary>
    public const int RepCenterBox = 1;
    /// <summary>Use average of all pixels in the box.</summary>
    public const int RepAverageColors = 2;
    /// <summary>Use average weighted by pixel count.</summary>
    public const int RepAveragePixels = 3;

    // Dithering / diffusion method (DIFFUSE_*)
    /// <summary>Choose diffusion method automatically.</summary>
    public const int DiffuseAuto = 0;
    /// <summary>No diffusion (nearest-color).</summary>
    public const int DiffuseNone = 1;
    /// <summary>Atkinson dithering.</summary>
    public const int DiffuseAtkinson = 2;
    /// <summary>Floyd-Steinberg dithering.</summary>
    public const int DiffuseFloydSteinberg = 3;
    /// <summary>Jarvis-Judice-Ninke dithering.</summary>
    public const int DiffuseJarvisJudiceNinke = 4;
    /// <summary>Stucki dithering.</summary>
    public const int DiffuseStucki = 5;
    /// <summary>Burkes dithering.</summary>
    public const int DiffuseBurkes = 6;

    // Quality mode (QUALITY_*)
    /// <summary>Automatic quality selection.</summary>
    public const int QualityAuto = 0;
    /// <summary>High quality (slower).</summary>
    public const int QualityHigh = 1;
    /// <summary>Low quality (faster).</summary>
    public const int QualityLow = 2;
    /// <summary>Full quality (slowest, most accurate).</summary>
    public const int QualityFull = 3;
    /// <summary>Highest quality mode.</summary>
    public const int QualityHighColor = 4;

    // Built-in palette identifiers (BUILTIN_*)
    /// <summary>Monochrome (black background) palette.</summary>
    public const int BuiltinMonoDark = 0;
    /// <summary>Monochrome (white background) palette.</summary>
    public const int BuiltinMonoLight = 1;
    /// <summary>xterm 16-color palette.</summary>
    public const int BuiltinXterm16 = 2;
    /// <summary>xterm 256-color palette.</summary>
    public const int BuiltinXterm256 = 3;
    /// <summary>VT340 16-color palette.</summary>
    public const int BuiltinVT340Mono = 4;
    /// <summary>VT340 16-color color palette.</summary>
    public const int BuiltinVT340Color = 5;
    /// <summary>1-bit grayscale palette.</summary>
    public const int BuiltinG1 = 6;
    /// <summary>2-bit grayscale palette.</summary>
    public const int BuiltinG2 = 7;
    /// <summary>4-bit grayscale palette.</summary>
    public const int BuiltinG4 = 8;
    /// <summary>8-bit grayscale palette.</summary>
    public const int BuiltinG8 = 9;
}
