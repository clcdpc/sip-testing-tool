param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')][string]$RuntimeIdentifier,
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
$null = Get-DesktopReleaseVersion -Version $Version
$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if ($outputPath.TrimEnd([IO.Path]::DirectorySeparatorChar) -eq $publishPath.TrimEnd([IO.Path]::DirectorySeparatorChar)) {
    throw 'Keep the package destination separate from the publish directory.'
}
if (-not $RuntimeIdentifier.StartsWith('osx-', [StringComparison]::Ordinal)) {
    foreach ($runtimeFile in @('System.Private.CoreLib.dll', 'coreclr.dll', 'libcoreclr.so', 'hostfxr.dll', 'libhostfxr.so')) {
        if (Test-Path -LiteralPath (Join-Path $publishPath $runtimeFile)) { throw "Bundled .NET runtime: $runtimeFile" }
    }
}
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$baseName = "SIP-Testing-Tool_${RuntimeIdentifier}_v$Version"
if ($RuntimeIdentifier -eq 'win-x64') {
    $files = @(Get-ChildItem -LiteralPath $publishPath -Recurse -File)
    if ($files.Count -ne 1 -or $files[0].Name -cne 'sip-testing-tool-avalonia.exe') {
        throw 'The Windows publish directory must contain exactly the single-file sip-testing-tool-avalonia.exe.'
    }
    $asset = Join-Path $outputPath "$baseName.zip"
    $temporaryAsset = Join-Path $outputPath (".package-" + [Guid]::NewGuid().ToString('N') + '.zip')
    try {
        $stream = [IO.File]::Open($temporaryAsset, [IO.FileMode]::CreateNew)
        try {
            $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
            try {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $files[0].FullName, $files[0].Name, [IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
            } finally { $archive.Dispose() }
        } finally { $stream.Dispose() }
        [IO.File]::Move($temporaryAsset, $asset, $true)
    } finally { [IO.File]::Delete($temporaryAsset) }
} elseif ($RuntimeIdentifier.StartsWith('osx-', [StringComparison]::Ordinal)) {
    if (-not $IsMacOS) { throw 'App bundle packaging requires macOS.' }
    $config = Get-Content -LiteralPath (Join-Path $publishPath 'sip-testing-tool-avalonia.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not $config.runtimeOptions.includedFrameworks -or $config.runtimeOptions.framework -or $config.runtimeOptions.frameworks) {
        throw 'The macOS app must include its .NET runtime.'
    }
    $asset = Join-Path $outputPath "$baseName.zip"
    $temporaryAsset = Join-Path $outputPath (".package-" + [Guid]::NewGuid().ToString('N') + '.zip')
    $scratch = [IO.Directory]::CreateTempSubdirectory('sip-macos-package-')
    try {
        $app = Join-Path $scratch.FullName 'SIP Testing Tool.app'
        & bash (Join-Path $PSScriptRoot 'PackageMacOS.sh') $publishPath $app $Version
        if ($LASTEXITCODE -ne 0) { throw 'macOS app packaging failed.' }
        # ditto preserves the bundle's relative links, executable bits and signature.
        & ditto -c -k --sequesterRsrc --keepParent $app $temporaryAsset
        if ($LASTEXITCODE -ne 0) { throw 'macOS ZIP creation failed.' }
        [IO.File]::Move($temporaryAsset, $asset, $true)
    } finally {
        [IO.File]::Delete($temporaryAsset)
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $scratch.FullName.StartsWith($temporaryRoot, [StringComparison]::Ordinal) -or
            -not $scratch.Name.StartsWith('sip-macos-package-', [StringComparison]::Ordinal)) {
            throw 'The package scratch directory is outside the expected temporary location.'
        }
        $scratch.Delete($true)
    }
} else {
    if (-not $IsLinux) { throw 'AppImage packaging requires Linux.' }
    $config = Get-Content -LiteralPath (Join-Path $publishPath 'sip-testing-tool-avalonia.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($config.runtimeOptions.framework.name -cne 'Microsoft.NETCore.App' -or $config.runtimeOptions.includedFrameworks) {
        throw 'The AppImage must require an installed .NET runtime.'
    }
    $asset = Join-Path $outputPath "$baseName.AppImage"
    $temporaryAsset = Join-Path $outputPath (".package-" + [Guid]::NewGuid().ToString('N') + '.AppImage')
    try {
        & bash (Join-Path $PSScriptRoot 'PackageAppImage.sh') $publishPath $temporaryAsset $Version
        if ($LASTEXITCODE -ne 0) { throw 'AppImage packaging failed.' }
        [IO.File]::Move($temporaryAsset, $asset, $true)
    } finally { [IO.File]::Delete($temporaryAsset) }
}
$hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$asset.sha256", "$hash  $([IO.Path]::GetFileName($asset))`n", [Text.UTF8Encoding]::new($false))
Write-Host "Created $asset ($((Get-Item -LiteralPath $asset).Length) bytes)"
