# libsixel

An experimental C# port of the SIXEL encoder and decoder from
[saitoha/libsixel](https://github.com/saitoha/libsixel), with a compiled
PowerShell cmdlet for rendering images in SIXEL-compatible terminals.

The project is currently released as a prerelease module for testing.

## PowerShell cmdlet: `Out-Sixel`

Repository now includes a separate project at `src/LibSixel.PowerShell` with a compiled PowerShell cmdlet:

```powershell
Out-Sixel -Path ./image.png
Out-Sixel -Path ./image.jpg
Out-Sixel -Path ./image.svg
```

It loads an image file (`.png`, `.jpg`/`.jpeg`, `.svg`), encodes it with the ported `LibSixel` code, and writes SIXEL data to the terminal.

### Install from PowerShell Gallery

```powershell
Install-Module -Name LibSixel.PowerShell -AllowPrerelease -Scope CurrentUser
Import-Module LibSixel.PowerShell
```

### Build and import

Source builds require the .NET 8 SDK or newer. The compiled module supports
PowerShell 7.4 or newer and targets .NET 8.

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

Use a SIXEL-compatible terminal, such as iTerm2 3.3 or newer or Windows
Terminal 1.22 or newer. Terminal support is independent of PowerShell support.

## License and attribution

This C# port is derived from
[saitoha/libsixel](https://github.com/saitoha/libsixel), Copyright (c) 2014-2016
Hayaki Saito. Both the original project and this port are distributed under the
MIT License. See [LICENSE](./LICENSE).
