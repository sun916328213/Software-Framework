[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
$visionRoot = Split-Path -Parent $PSScriptRoot
$controls = Join-Path $visionRoot 'Vision.Controls'
$files = @('View/BlobParametersView.xaml', 'View/BlobParametersView.xaml.cs', 'Presentation/PropertyGridThemeAssist.cs') |
    ForEach-Object { [IO.Path]::GetFullPath((Join-Path $controls $_)) }
# These files are already excluded from compilation. Do not delete other compatibility hosts.
$references = Get-ChildItem -LiteralPath $controls -Recurse -File |
    Where-Object { $_.Extension -in @('.cs', '.xaml') -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.FullName -notin $files } |
    Select-String -Pattern 'BlobParametersView|PropertyGridThemeAssist'
if ($references) { $references | Out-Host; throw 'Production references remain. Nothing was removed.' }
foreach ($file in $files) {
    if ((Test-Path -LiteralPath $file) -and $PSCmdlet.ShouldProcess($file, 'Remove retired editor source')) {
        Remove-Item -LiteralPath $file
    }
}
