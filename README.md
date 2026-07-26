# libsixel

## PowerShell cmdlet: `Out-Sixel`

Repository now includes a separate project at `src/LibSixel.PowerShell` with a compiled PowerShell cmdlet:

```powershell
Out-Sixel -Path ./image.png
Out-Sixel -Path ./image.jpg
Out-Sixel -Path ./image.svg
```

It loads an image file (`.png`, `.jpg`/`.jpeg`, `.svg`), encodes it with the ported `LibSixel` code, and writes SIXEL data to the terminal.

### Build and import

```powershell
dotnet build ./src/LibSixel.PowerShell/LibSixel.PowerShell.csproj
Import-Module ./src/LibSixel.PowerShell/bin/Debug/net8.0/LibSixel.PowerShell.psd1
```

### Usage examples

```powershell
Out-Sixel -Path ./image.png
Out-Sixel -Path ./image.jpg
Out-Sixel -Path ./image.svg
Out-Sixel -Path ./image.png -Colors 128
Out-Sixel -Path ./image.png -AsString | Set-Content ./image.sixel -NoNewline
```

### Decoding notes

`LibSixel.PowerShell` uses SkiaSharp to decode/rasterize input images before SIXEL encoding.

Current supported formats:
- PNG (`.png`)
- JPEG (`.jpg`, `.jpeg`)
- SVG (`.svg`)
