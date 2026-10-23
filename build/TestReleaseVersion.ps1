$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
$repository = Split-Path -Parent $PSScriptRoot
$cases = Get-Content -LiteralPath (Join-Path $repository 'tests/release-version-cases.json') -Raw | ConvertFrom-Json
foreach ($case in $cases) {
    foreach ($tag in @($false, $true)) {
        $inputVersion = if ($tag) { 'v' + $case.version } else { $case.version }
        $parsed = $null
        try { $parsed = Get-DesktopReleaseVersion -Version $inputVersion -Tag:$tag } catch {
            if ($case.valid) { throw }
        }
        if (($null -ne $parsed) -ne $case.valid) { throw "Unexpected version validation: $inputVersion" }
        if ($case.valid -and ($parsed.Version -cne $case.version -or $parsed.IsPrerelease -ne $case.prerelease)) {
            throw "Unexpected version classification: $inputVersion"
        }
    }
}
foreach ($invalidTag in @('2.1.2-beta.1', 'V2.1.2-beta.1', 'release-2.1.2-beta.1')) {
    $rejected = $false
    try { $null = Get-DesktopReleaseVersion -Version $invalidTag -Tag } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid release tag accepted: $invalidTag" }
}

# Exercise the production packager/verifier with the same names CI uploads; no publish or network is needed.
$scratch = [IO.Directory]::CreateTempSubdirectory('sip-release-tests-')
try {
    $publish = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName 'publish')).FullName
    [IO.File]::WriteAllText((Join-Path $publish 'sip-testing-tool-avalonia.exe'), 'release packaging fixture')
    foreach ($version in @('2.1.2', '2.1.2-beta.1', '2.1.2-beta.10+build.001')) {
        $output = Join-Path $scratch.FullName $version
        & (Join-Path $PSScriptRoot 'PackageDesktop.ps1') -RuntimeIdentifier win-x64 -PublishDirectory $publish -OutputDirectory $output -Version $version
        & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier win-x64 -Version $version
    }
    # Portable fixtures exercise architecture, runtime, version and checksum rejection.
    # Mac jobs verify real ditto archives and code signatures after publishing instead.
    if (-not $IsMacOS) {
        foreach ($rid in @('osx-arm64', 'osx-x64')) {
            foreach ($version in @('2.1.2', '2.1.2-beta.1', '2.1.2-beta.10+build.001')) {
                foreach ($scenario in @('valid', 'wrong-cpu', 'missing-runtime', 'wrong-bundle-version', 'bad-checksum')) {
                    $output = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName "$rid/$version/$scenario")).FullName
                    $asset = Join-Path $output "SIP-Testing-Tool_${rid}_v$version.zip"
                    $cpu = if (($rid -eq 'osx-arm64') -xor ($scenario -eq 'wrong-cpu')) { 0x0c } else { 0x07 }
                    $bundleVersion = if ($scenario -eq 'wrong-bundle-version') { '0.0.0' } else { ($version -split '[-+]', 2)[0] }
                    $files = @{
                        'Info.plist' = (Get-Content -LiteralPath (Join-Path $repository 'src/sip-testing-tool-avalonia/Properties/macOS/Info.plist') -Raw).Replace('__APP_VERSION__', $bundleVersion)
                        'MacOS/sip-testing-tool-avalonia' = [byte[]]@(0xcf, 0xfa, 0xed, 0xfe, $cpu, 0, 0, 1)
                        'Resources/app.icns' = 'icon fixture'
                        'Resources/sip-testing-tool-avalonia.dll' = 'assembly fixture'
                        'Resources/System.Private.CoreLib.dll' = 'runtime fixture'
                        'Resources/sip-testing-tool-avalonia.runtimeconfig.json' = '{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"}]}}'
                        'Frameworks/libcoreclr.dylib' = 'runtime fixture'
                        'Frameworks/libhostfxr.dylib' = 'runtime fixture'
                        'Frameworks/libhostpolicy.dylib' = 'runtime fixture'
                        '_CodeSignature/CodeResources' = 'signature fixture'
                    }
                    if ($scenario -eq 'missing-runtime') { $files.Remove('Resources/System.Private.CoreLib.dll') }
                    $zip = [IO.Compression.ZipFile]::Open($asset, [IO.Compression.ZipArchiveMode]::Create)
                    try {
                        foreach ($file in $files.GetEnumerator()) {
                            $stream = $zip.CreateEntry('SIP Testing Tool.app/Contents/' + $file.Key).Open()
                            try {
                                $bytes = if ($file.Value -is [byte[]]) { $file.Value } else { [Text.Encoding]::UTF8.GetBytes($file.Value) }
                                $stream.Write($bytes, 0, $bytes.Length)
                            } finally { $stream.Dispose() }
                        }
                    } finally { $zip.Dispose() }
                    $hash = if ($scenario -eq 'bad-checksum') { '0' * 64 } else { (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant() }
                    [IO.File]::WriteAllText("$asset.sha256", "$hash  $([IO.Path]::GetFileName($asset))`n")
                    $rejected = $false
                    try {
                        & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier $rid -Version $version
                    } catch {
                        if ($scenario -eq 'valid') { throw }
                        $rejected = $true
                    }
                    if (-not $rejected -and $scenario -ne 'valid') { throw "Accepted invalid macOS release: $rid/$version/$scenario" }
                }
            }
        }
    }
} finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $scratch.FullName.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $scratch.Name.StartsWith('sip-release-tests-', [StringComparison]::Ordinal)) {
        throw 'The test scratch directory is outside the expected temporary location.'
    }
    $scratch.Delete($true)
}
Write-Host "Verified $($cases.Count) version cases, release tag validation, and stable/beta package names and checksums."
