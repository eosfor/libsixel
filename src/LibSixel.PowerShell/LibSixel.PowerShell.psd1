@{
    RootModule = 'LibSixel.PowerShell.dll'
    ModuleVersion = '0.2.0'
    GUID = '4d2bc0f5-12fd-4d3d-bff7-cfaee2f35425'
    Author = 'Andrey Vernigora'
    CompanyName = ''
    Copyright = '(c) 2026 Andrey Vernigora. MIT License.'
    Description = 'PowerShell cmdlets for rendering images as SIXEL terminal graphics.'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')
    CmdletsToExport = @('Out-Sixel')
    AliasesToExport = @()
    FunctionsToExport = @()
    VariablesToExport = @()
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta2'
            Tags = @('SIXEL', 'Terminal', 'Image')
            ProjectUri = 'https://github.com/eosfor/libsixel'
            LicenseUri = 'https://github.com/eosfor/libsixel/blob/main/LICENSE'
            ReleaseNotes = 'Adds public licensing, upstream attribution, author metadata, and installation documentation.'
        }
    }
}
