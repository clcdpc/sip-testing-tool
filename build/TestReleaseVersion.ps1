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

$project = Join-Path $repository 'src/sip-testing-tool-avalonia/sip-testing-tool-avalonia.csproj'
function Get-ProjectVersionProperties {
    param([string[]]$Properties = @())
    $result = & dotnet msbuild $project -nologo -target:GetAssemblyVersion -getProperty:Version,InformationalVersion @Properties
    if ($LASTEXITCODE -ne 0) { throw "Could not evaluate application version properties: $result" }
    ($result | Out-String | ConvertFrom-Json).Properties
}
$development = Get-ProjectVersionProperties
if ($development.Version -cne '0.0.0-dev' -or $development.InformationalVersion -cne '0.0.0-dev') {
    throw 'Ordinary local builds must default to the 0.0.0-dev informational version.'
}
$versions = @('0.0.0-dev', '3.0.0', '3.0.0-beta.5', '3.0.0-rc.1', '3.0.0-beta.10+build.001', '3.0.0+build.001')
foreach ($profile in @('Windows-Zip', 'Linux-AppImage', 'macOS-arm64', 'macOS-x64')) {
    $properties = Get-ProjectVersionProperties -Properties @('-p:Version=3.0.0-beta.5+build.001', "-p:PublishProfile=$profile")
    if ($properties.Version -cne '3.0.0-beta.5+build.001' -or $properties.InformationalVersion -cne $properties.Version) {
        throw "The $profile profile must retain the explicitly supplied application version."
    }
}

# Compile tiny versioned assemblies for the production verifier and packaging fixtures.
# No application publish, release tag, GitHub environment or network is needed.
$scratch = [IO.Directory]::CreateTempSubdirectory('sip-release-tests-')
try {
    $assemblies = @{}
    foreach ($version in $versions) {
        $releaseVersion = Get-DesktopReleaseVersion -Version "v$version" -Tag
        $properties = Get-ProjectVersionProperties -Properties @("-p:Version=$($releaseVersion.Version)")
        if ($properties.Version -cne $version -or $properties.InformationalVersion -cne $version) {
            throw "MSBuild did not receive the complete tag-derived version: $version"
        }
        foreach ($includeRevision in @($false, $true)) {
            $informationalVersion = $properties.InformationalVersion
            if ($includeRevision) {
                $separator = if ($version.Contains('+')) { '.' } else { '+' }
                $informationalVersion += $separator + ('a' * 40)
            }
            $assembly = Join-Path $scratch.FullName "$version-$includeRevision.dll"
            $source = '[assembly: System.Reflection.AssemblyInformationalVersion("{0}")] public sealed class Fixture{1}{2} {{}}' -f $informationalVersion, $assemblies.Count, $includeRevision
            Add-Type -TypeDefinition $source -OutputAssembly $assembly
            $assemblies[$version] = $assembly
            & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -ApplicationPath $assembly -Version $version
        }
    }
    $publish = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName 'publish')).FullName
    foreach ($version in $versions) {
        [IO.File]::Copy($assemblies[$version], (Join-Path $publish 'sip-testing-tool-avalonia.exe'), $true)
        $output = Join-Path $scratch.FullName $version
        & (Join-Path $PSScriptRoot 'PackageDesktop.ps1') -RuntimeIdentifier win-x64 -PublishDirectory $publish -OutputDirectory $output -Version $version
        & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier win-x64 -Version $version
    }
    # Every platform must reject a packaging version different from the compiled payload,
    # before invoking platform-specific packaging tools or writing a download.
    [IO.File]::Copy($assemblies['3.0.0-beta.5'], (Join-Path $publish 'sip-testing-tool-avalonia.exe'), $true)
    [IO.File]::Copy($assemblies['3.0.0-beta.5'], (Join-Path $publish 'sip-testing-tool-avalonia.dll'), $true)
    foreach ($rid in @('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')) {
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'PackageDesktop.ps1') -RuntimeIdentifier $rid -PublishDirectory $publish -OutputDirectory (Join-Path $scratch.FullName "mismatch-$rid") -Version '3.0.0-beta.6'
        } catch {
            if ($_.Exception.Message -notlike '*informational version*') { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw "Packaging accepted an application version mismatch: $rid" }
    }
    foreach ($expected in @('3.0.0', '3.0.0-beta.10', '3.0.0-beta.10+build.002')) {
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -ApplicationPath $assemblies['3.0.0-beta.10+build.001'] -Version $expected
        } catch { $rejected = $true }
        if (-not $rejected) { throw "Application verification ignored a prerelease or metadata mismatch: $expected" }
    }
    # Portable fixtures exercise architecture, runtime, version and checksum rejection.
    # Mac jobs verify real ditto archives and code signatures after publishing instead.
    if (-not $IsMacOS) {
        foreach ($rid in @('osx-arm64', 'osx-x64')) {
            foreach ($version in @('0.0.0-dev', '3.0.0', '3.0.0-beta.5', '3.0.0-beta.10+build.001')) {
                foreach ($scenario in @('valid', 'wrong-cpu', 'missing-runtime', 'wrong-bundle-version', 'wrong-application-version', 'bad-checksum')) {
                    $output = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName "$rid/$version/$scenario")).FullName
                    $asset = Join-Path $output "SIP-Testing-Tool_${rid}_v$version.zip"
                    $cpu = if (($rid -eq 'osx-arm64') -xor ($scenario -eq 'wrong-cpu')) { 0x0c } else { 0x07 }
                    $bundleVersion = if ($scenario -eq 'wrong-bundle-version') { '9.9.9' } else { ($version -split '[-+]', 2)[0] }
                    $files = @{
                        'Info.plist' = (Get-Content -LiteralPath (Join-Path $repository 'src/sip-testing-tool-avalonia/Properties/macOS/Info.plist') -Raw).Replace('__APP_VERSION__', $bundleVersion)
                        'MacOS/sip-testing-tool-avalonia' = [byte[]]@(0xcf, 0xfa, 0xed, 0xfe, $cpu, 0, 0, 1)
                        'Resources/app.icns' = 'icon fixture'
                        'Resources/sip-testing-tool-avalonia.dll' = [IO.File]::ReadAllBytes($assemblies[$version])
                        'Resources/System.Private.CoreLib.dll' = 'runtime fixture'
                        'Resources/sip-testing-tool-avalonia.runtimeconfig.json' = '{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"}]}}'
                        'Frameworks/libcoreclr.dylib' = 'runtime fixture'
                        'Frameworks/libhostfxr.dylib' = 'runtime fixture'
                        'Frameworks/libhostpolicy.dylib' = 'runtime fixture'
                        '_CodeSignature/CodeResources' = 'signature fixture'
                    }
                    if ($scenario -eq 'missing-runtime') { $files.Remove('Resources/System.Private.CoreLib.dll') }
                    if ($scenario -eq 'wrong-application-version') {
                        $different = if ($version -eq '0.0.0-dev') { '3.0.0' } else { '0.0.0-dev' }
                        $files['Resources/sip-testing-tool-avalonia.dll'] = [IO.File]::ReadAllBytes($assemblies[$different])
                    }
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
Write-Host "Verified $($cases.Count) version cases, tag-to-MSBuild injection, development defaults, all release profiles, embedded versions, and package names/checksums."
