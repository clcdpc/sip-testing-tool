param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64')][string]$RuntimeIdentifier,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
$null = Get-DesktopReleaseVersion -Version $Version
$extension = if ($RuntimeIdentifier -eq 'win-x64') { 'zip' } else { 'AppImage' }
$name = "SIP-Testing-Tool_${RuntimeIdentifier}_v$Version.$extension"
$files = @(Get-ChildItem -LiteralPath $Directory -Recurse -File)
if ($files.Count -ne 2 -or $name -cnotin $files.Name -or "$name.sha256" -cnotin $files.Name) {
    throw "Expected only $name and its SHA-256 sidecar."
}
$asset = Join-Path $Directory $name
if ((Get-Item -LiteralPath $asset).Length -eq 0) { throw 'The release download is empty.' }
$actualHash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = (Get-Content -LiteralPath "$asset.sha256" -Raw).TrimEnd()
if ($checksum -cne "$actualHash  $name") { throw "SHA-256 mismatch: $name" }
if ($RuntimeIdentifier -eq 'win-x64') {
    $zip = [IO.Compression.ZipFile]::OpenRead($asset)
    try {
        if ($zip.Entries.Count -ne 1 -or $zip.Entries[0].FullName -cne 'sip-testing-tool-avalonia.exe' -or $zip.Entries[0].Length -eq 0) {
            throw 'The Windows ZIP must contain exactly one application EXE at its root.'
        }
    } finally { $zip.Dispose() }
} else {
    $stream = [IO.File]::OpenRead($asset)
    try {
        $header = [byte[]]::new(11)
        $stream.ReadExactly($header)
        if ($header[0] -ne 0x7f -or [Text.Encoding]::ASCII.GetString($header, 1, 3) -cne 'ELF' -or $header[8] -ne 0x41 -or $header[9] -ne 0x49 -or $header[10] -ne 2) {
            throw 'The Linux download is not a type 2 AppImage.'
        }
    } finally { $stream.Dispose() }
}
Write-Host "Verified $name"
