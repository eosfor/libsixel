namespace LibSixel;

/// <summary>
/// Error-diffusion (dithering) method used when mapping pixels to a palette.
/// </summary>
public enum DiffuseMethod
{
    /// <summary>Select the method automatically.</summary>
    Auto = 0,

    /// <summary>No diffusion — nearest palette colour only.</summary>
    None = 1,

    /// <summary>Atkinson dithering.</summary>
    Atkinson = 2,

    /// <summary>Floyd-Steinberg error diffusion.</summary>
    FloydSteinberg = 3,

    /// <summary>Jarvis-Judice-Ninke error diffusion.</summary>
    JarvisJudiceNinke = 4,

    /// <summary>Stucki error diffusion.</summary>
    Stucki = 5,

    /// <summary>Burkes error diffusion.</summary>
    Burkes = 6,
}
