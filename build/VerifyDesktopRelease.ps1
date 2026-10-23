param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')][string]$RuntimeIdentifier,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
$null = Get-DesktopReleaseVersion -Version $Version
$extension = if ($RuntimeIdentifier -eq 'linux-x64') { 'AppImage' } else { 'zip' }
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
} elseif ($RuntimeIdentifier.StartsWith('osx-', [StringComparison]::Ordinal)) {
    $zip = [IO.Compression.ZipFile]::OpenRead($asset)
    try {
        $root = 'SIP Testing Tool.app/Contents/'
        foreach ($path in @('Info.plist', 'MacOS/sip-testing-tool-avalonia', 'Resources/app.icns',
            'Resources/sip-testing-tool-avalonia.dll', 'Resources/System.Private.CoreLib.dll',
            'Resources/sip-testing-tool-avalonia.runtimeconfig.json', 'Frameworks/libcoreclr.dylib',
            'Frameworks/libhostfxr.dylib', 'Frameworks/libhostpolicy.dylib', '_CodeSignature/CodeResources')) {
            $entry = $zip.GetEntry($root + $path)
            if ($null -eq $entry -or $entry.Length -eq 0) { throw "Missing macOS bundle file: $path" }
        }
        foreach ($entry in $zip.Entries) {
            if (($entry.FullName -cnotlike 'SIP Testing Tool.app/*' -and $entry.FullName -cnotlike '__MACOSX/*') -or
                $entry.FullName -cmatch '(^|/)\.\.(/|$)') {
                throw "Unexpected macOS ZIP entry: $($entry.FullName)"
            }
        }
        $stream = $zip.GetEntry($root + 'MacOS/sip-testing-tool-avalonia').Open()
        try {
            $header = [byte[]]::new(8)
            $stream.ReadExactly($header)
            $expectedCpu = if ($RuntimeIdentifier -eq 'osx-arm64') { 0x0100000c } else { 0x01000007 }
            if ([BitConverter]::ToUInt32($header, 0) -ne 0xfeedfacfL -or [BitConverter]::ToUInt32($header, 4) -ne $expectedCpu) {
                throw "The macOS executable does not match $RuntimeIdentifier."
            }
        } finally { $stream.Dispose() }
        $reader = [IO.StreamReader]::new($zip.GetEntry($root + 'Info.plist').Open())
        try { [xml]$plist = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $bundleVersion = ($Version -split '[-+]', 2)[0]
        foreach ($key in @('CFBundleVersion', 'CFBundleShortVersionString')) {
            $value = $plist.SelectSingleNode("/plist/dict/key[.='$key']/following-sibling::*[1]")
            if ($null -eq $value -or $value.InnerText -cne $bundleVersion) { throw "Incorrect macOS $key." }
        }
        $reader = [IO.StreamReader]::new($zip.GetEntry($root + 'Resources/sip-testing-tool-avalonia.runtimeconfig.json').Open())
        try { $config = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if (-not $config.runtimeOptions.includedFrameworks -or $config.runtimeOptions.framework -or $config.runtimeOptions.frameworks) {
            throw 'The macOS download must include its .NET runtime.'
        }
    } finally { $zip.Dispose() }
    if ($IsMacOS) {
        $scratch = [IO.Directory]::CreateTempSubdirectory('sip-macos-verify-')
        try {
            & ditto -x -k $asset $scratch.FullName
            if ($LASTEXITCODE -ne 0) { throw 'Could not extract the macOS release.' }
            & codesign --verify --deep --strict (Join-Path $scratch.FullName 'SIP Testing Tool.app')
            if ($LASTEXITCODE -ne 0) { throw 'The extracted macOS signature is invalid.' }
        } finally {
            $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            if (-not $scratch.FullName.StartsWith($temporaryRoot, [StringComparison]::Ordinal) -or
                -not $scratch.Name.StartsWith('sip-macos-verify-', [StringComparison]::Ordinal)) {
                throw 'The verification scratch directory is outside the expected temporary location.'
            }
            $scratch.Delete($true)
        }
    }
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
