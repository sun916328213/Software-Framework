$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path 'Themes\Controls.xaml').Path)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
$lines = $text -split "`r?`n"
for ($i = 198; $i -lt 215; $i++) {
    $line = $lines[$i]
    $codes = ($line.ToCharArray() | ForEach-Object { '{0:X4}' -f [int]$_ }) -join ' '
    Write-Output ("LINE {0}: {1}" -f ($i + 1), $codes)
}
