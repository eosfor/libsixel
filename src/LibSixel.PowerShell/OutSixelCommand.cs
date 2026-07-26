using LibSixel;
using LibSixel.PowerShell.Internal;
using System.Management.Automation;

namespace LibSixel.PowerShell;

/// <summary>
/// Renders PNG/JPG/SVG files directly to SIXEL output in the current terminal.
/// </summary>
[Cmdlet(VerbsData.Out, "Sixel")]
[OutputType(typeof(string))]
public sealed class OutSixelCommand : PSCmdlet
{
    /// <summary>
    /// Image path(s) to render (.png, .jpg/.jpeg, .svg), or SVG content supplied through the pipeline.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName", "PSPath", "LiteralPath")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Palette size used for quantization (2..256).
    /// </summary>
    [Parameter]
    [ValidateRange(2, 256)]
    public int Colors { get; set; } = 256;

    /// <summary>
    /// Target output width in pixels. If Height is omitted, aspect ratio is preserved.
    /// </summary>
    [Parameter]
    [ValidateRange(1, 10000)]
    public int? Width { get; set; }

    /// <summary>
    /// Target output height in pixels. If Width is omitted, aspect ratio is preserved.
    /// </summary>
    [Parameter]
    [ValidateRange(1, 10000)]
    public int? Height { get; set; }

    /// <summary>
    /// Emits SIXEL body only (without DCS wrapper).
    /// </summary>
    [Parameter]
    public SwitchParameter BodyOnly { get; set; }

    /// <summary>
    /// Writes SIXEL text into the pipeline instead of writing to host UI.
    /// </summary>
    [Parameter]
    public SwitchParameter AsString { get; set; }

    /// <summary>
    /// Prevents appending a trailing newline when writing to host UI.
    /// </summary>
    [Parameter]
    public SwitchParameter NoNewline { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        foreach (string path in Path)
        {
            RenderPathOrSvgContent(path);
        }
    }

    private void RenderPathOrSvgContent(string pathOrContent)
    {
        try
        {
            if (LooksLikeSvgContent(pathOrContent))
            {
                EmitSixel(ImageDecoder.DecodeSvgContent(pathOrContent));
                return;
            }

            string resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(pathOrContent);
            EmitSixel(ImageDecoder.DecodeFile(resolvedPath));
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "OutSixelFailed", ErrorCategory.InvalidData, pathOrContent));
        }
    }

    private static bool LooksLikeSvgContent(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.TrimStart();
        return trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
    }

    private void EmitSixel(DecodedImage image)
    {
        image = ImageDecoder.Resize(image, Width, Height);

        using var dither = SixelDither.CreateFromImage(
            image.Pixels, image.Width, image.Height, PixelFormat.RGBA8888, Colors);

        dither.PixelFormat = PixelFormat.RGBA8888;
        dither.BodyOnly = BodyOnly.IsPresent;

        string sixel = SixelEncoder.EncodeToString(
            image.Pixels, image.Width, image.Height, PixelFormat.RGBA8888, dither);

        if (AsString.IsPresent)
        {
            WriteObject(sixel);
            return;
        }

        Host.UI.Write(sixel);
        if (!NoNewline.IsPresent)
        {
            Host.UI.WriteLine();
        }
    }
}
