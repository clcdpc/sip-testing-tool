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

$project = Join-Path $repository 'src/sip-testing-tool/sip-testing-tool.csproj'
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
function Assert-InvalidPackageFiles {
    param([string]$ValidDirectory, [string]$RuntimeIdentifier, [string]$Version)
    $package = @(Get-ChildItem -LiteralPath $ValidDirectory -File)
    if ($package.Count -ne 1 -or $package[0].Extension -eq '.sha256') { throw 'Packaging must produce exactly one package without a sidecar.' }
    foreach ($scenario in @('missing', 'empty', 'malformed', 'wrong-name', 'extra-sidecar', 'extra-hidden-file')) {
        $output = [IO.Directory]::CreateDirectory("$ValidDirectory-invalid-$scenario").FullName
        $asset = Join-Path $output $package[0].Name
        [IO.File]::Copy($package[0].FullName, $asset)
        switch ($scenario) {
            'missing' { [IO.File]::Delete($asset) }
            'empty' { [IO.File]::WriteAllText($asset, '') }
            'malformed' { [IO.File]::WriteAllText($asset, 'This is not an archive or AppImage.') }
            'wrong-name' { [IO.File]::Move($asset, (Join-Path $output 'incorrect-version.zip')) }
            'extra-sidecar' { [IO.File]::WriteAllText("$asset.sha256", 'obsolete checksum') }
            'extra-hidden-file' {
                $extra = Join-Path $output '.unexpected'
                [IO.File]::WriteAllText($extra, 'extra')
                if ($IsWindows) { [IO.File]::SetAttributes($extra, [IO.FileAttributes]::Hidden) }
            }
        }
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier $RuntimeIdentifier -Version $Version
        } catch { $rejected = $true }
        if (-not $rejected) { throw "Accepted invalid $RuntimeIdentifier package: $scenario" }
    }
}
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
        [IO.File]::Copy($assemblies[$version], (Join-Path $publish 'sip-testing-tool.exe'), $true)
        $output = Join-Path $scratch.FullName $version
        & (Join-Path $PSScriptRoot 'PackageDesktop.ps1') -RuntimeIdentifier win-x64 -PublishDirectory $publish -OutputDirectory $output -Version $version
        & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier win-x64 -Version $version
        Assert-InvalidPackageFiles -ValidDirectory $output -RuntimeIdentifier win-x64 -Version $version
    }
    if ($IsWindows) {
        $output = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName 'wrong-windows-payload')).FullName
        $asset = Join-Path $output 'SIP-Testing-Tool_win-x64_v3.0.0.zip'
        $zip = [IO.Compression.ZipFile]::Open($asset, [IO.Compression.ZipArchiveMode]::Create)
        try {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $assemblies['3.0.0-beta.5'], 'sip-testing-tool.exe') | Out-Null
        } finally { $zip.Dispose() }
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier win-x64 -Version '3.0.0'
        } catch {
            if ($_.Exception.Message -notlike '*informational version*') { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw 'Windows ZIP verification ignored its embedded version.' }
    }
    # Exercise the Linux runtime guard on every host, without publishing the app.
    $linuxPublish = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName 'linux-publish')).FullName
    $apphost = [byte[]]::new(20)
    [Text.Encoding]::ASCII.GetBytes("`u{007f}ELF").CopyTo($apphost, 0)
    $apphost[4] = 2
    $apphost[5] = 1
    $apphost[18] = 62
    $linuxConfig = '{"runtimeOptions":{"tfm":"net10.0","includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"}]}}'
    $frameworkDependentConfig = '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}'
    $linuxFiles = @{
        'sip-testing-tool' = $apphost
        'sip-testing-tool.dll' = [IO.File]::ReadAllBytes($assemblies['0.0.0-dev'])
        'sip-testing-tool.deps.json' = '{}'
        'sip-testing-tool.runtimeconfig.json' = $linuxConfig
        'System.Private.CoreLib.dll' = 'runtime fixture'
        'libcoreclr.so' = 'runtime fixture'
        'libhostfxr.so' = 'runtime fixture'
        'libhostpolicy.so' = 'runtime fixture'
    }
    function Restore-LinuxPublishFixture {
        foreach ($file in $linuxFiles.GetEnumerator()) {
            $bytes = if ($file.Value -is [byte[]]) { $file.Value } else { [Text.Encoding]::UTF8.GetBytes($file.Value) }
            [IO.File]::WriteAllBytes((Join-Path $linuxPublish $file.Key), $bytes)
        }
    }
    foreach ($scenario in @('valid', 'framework-dependent', 'frameworks', 'wrong-runtime', 'non-native-apphost') + @($linuxFiles.Keys)) {
        Restore-LinuxPublishFixture
        $configPath = Join-Path $linuxPublish 'sip-testing-tool.runtimeconfig.json'
        switch ($scenario) {
            'framework-dependent' { [IO.File]::WriteAllText($configPath, $frameworkDependentConfig) }
            'frameworks' { [IO.File]::WriteAllText($configPath, $linuxConfig.Replace('includedFrameworks', 'frameworks')) }
            'wrong-runtime' { [IO.File]::WriteAllText($configPath, $linuxConfig.Replace('10.0', '9.0')) }
            'non-native-apphost' { [IO.File]::WriteAllText((Join-Path $linuxPublish 'sip-testing-tool'), "#!/bin/sh`nexit 0 # not a native apphost`n") }
            default {
                if ($scenario -ne 'valid') { [IO.File]::Delete((Join-Path $linuxPublish $scenario)) }
            }
        }
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'VerifyLinuxPublish.ps1') -Directory $linuxPublish
        } catch {
            if ($scenario -eq 'valid') { throw }
            $rejected = $true
        }
        if (-not $rejected -and $scenario -ne 'valid') { throw "Accepted invalid Linux publish output: $scenario" }
    }
    Restore-LinuxPublishFixture
    if ($IsLinux) {
        # A normal dotnet publish creates the packager's scratch parent first.
        $null = [IO.Directory]::CreateDirectory((Join-Path $repository 'artifacts/avalonia'))
        foreach ($version in @('0.0.0-dev', '3.0.0-beta.10+build.001')) {
            [IO.File]::Copy($assemblies[$version], (Join-Path $linuxPublish 'sip-testing-tool.dll'), $true)
            $output = Join-Path $scratch.FullName "linux-$version"
            & (Join-Path $PSScriptRoot 'PackageDesktop.ps1') -RuntimeIdentifier linux-x64 -PublishDirectory $linuxPublish -OutputDirectory $output -Version $version
            & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier linux-x64 -Version $version
            Assert-InvalidPackageFiles -ValidDirectory $output -RuntimeIdentifier linux-x64 -Version $version
        }
        # Bypass the PowerShell packager's guards to exercise the packaged-byte checks.
        foreach ($scenario in @('wrong-application-version', 'wrong-metadata-version', 'missing-runtime', 'framework-dependent', 'dotnet-launcher')) {
            Restore-LinuxPublishFixture
            $version = '3.0.0-beta.10+build.001'
            $payloadVersion = if ($scenario -eq 'wrong-application-version') { '0.0.0-dev' } else { $version }
            $metadataVersion = if ($scenario -eq 'wrong-metadata-version') { '9.9.9' } else { $version }
            [IO.File]::Copy($assemblies[$payloadVersion], (Join-Path $linuxPublish 'sip-testing-tool.dll'), $true)
            if ($scenario -eq 'missing-runtime') { [IO.File]::Delete((Join-Path $linuxPublish 'libcoreclr.so')) }
            if ($scenario -eq 'framework-dependent') {
                [IO.File]::WriteAllText((Join-Path $linuxPublish 'sip-testing-tool.runtimeconfig.json'), $frameworkDependentConfig)
            }
            $output = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName $scenario)).FullName
            $asset = Join-Path $output "SIP-Testing-Tool_linux-x64_v$version.AppImage"
            & bash (Join-Path $PSScriptRoot 'PackageAppImage.sh') $linuxPublish $asset $metadataVersion
            if ($LASTEXITCODE -ne 0) { throw 'Could not build the invalid AppImage fixture.' }
            if ($scenario -eq 'dotnet-launcher') {
                $fixture = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName 'launcher-fixture')).FullName
                Push-Location -LiteralPath $fixture
                try {
                    & $asset --appimage-extract > $null
                    if ($LASTEXITCODE -ne 0) { throw 'Could not extract the launcher fixture.' }
                } finally { Pop-Location }
                $appDir = Join-Path $fixture 'squashfs-root'
                $launcherPath = Join-Path $appDir 'AppRun'
                $launcher = (Get-Content -LiteralPath $launcherPath -Raw).Replace('exec "$app_dir/usr/bin/sip-testing-tool" "$@"', 'exec dotnet "$app_dir/usr/bin/sip-testing-tool.dll" "$@"')
                [IO.File]::WriteAllText($launcherPath, $launcher)
                $tools = Join-Path $repository 'artifacts/tools/appimage'
                $previousArch = $env:ARCH
                $previousExtractAndRun = $env:APPIMAGE_EXTRACT_AND_RUN
                $env:ARCH = 'x86_64'
                $env:APPIMAGE_EXTRACT_AND_RUN = '1'
                try {
                    & (Join-Path $tools 'appimagetool-1.9.1-x86_64.AppImage') --runtime-file (Join-Path $tools 'runtime-20251108-x86_64') --no-appstream $appDir $asset
                    if ($LASTEXITCODE -ne 0) { throw 'Could not repackage the launcher fixture.' }
                } finally {
                    $env:ARCH = $previousArch
                    $env:APPIMAGE_EXTRACT_AND_RUN = $previousExtractAndRun
                }
            }
            $rejected = $false
            try {
                & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier linux-x64 -Version $version
            } catch {
                $expectedError = switch ($scenario) {
                    'missing-runtime' { '*libcoreclr.so*' }
                    'framework-dependent' { '*self-contained runtime configuration*' }
                    'dotnet-launcher' { '*AppRun*' }
                    default { '*version*' }
                }
                if ($_.Exception.Message -notlike $expectedError) { throw }
                $rejected = $true
            }
            if (-not $rejected) { throw "Accepted an invalid Linux package: $scenario" }
        }
    }
    # Every platform must reject a packaging version different from the compiled payload,
    # before invoking platform-specific packaging tools or writing a download.
    [IO.File]::Copy($assemblies['3.0.0-beta.5'], (Join-Path $publish 'sip-testing-tool.exe'), $true)
    [IO.File]::Copy($assemblies['3.0.0-beta.5'], (Join-Path $publish 'sip-testing-tool.dll'), $true)
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
    # Portable fixtures exercise architecture, runtime and embedded version rejection.
    # Mac jobs verify real ditto archives and code signatures after publishing instead.
    if (-not $IsMacOS) {
        foreach ($rid in @('osx-arm64', 'osx-x64')) {
            foreach ($version in @('0.0.0-dev', '3.0.0', '3.0.0-beta.5', '3.0.0-beta.10+build.001')) {
                foreach ($scenario in @('valid', 'wrong-cpu', 'missing-runtime', 'wrong-bundle-version', 'wrong-application-version')) {
                    $output = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName "$rid/$version/$scenario")).FullName
                    $asset = Join-Path $output "SIP-Testing-Tool_${rid}_v$version.zip"
                    $cpu = if (($rid -eq 'osx-arm64') -xor ($scenario -eq 'wrong-cpu')) { 0x0c } else { 0x07 }
                    $bundleVersion = if ($scenario -eq 'wrong-bundle-version') { '9.9.9' } else { ($version -split '[-+]', 2)[0] }
                    $files = @{
                        'Info.plist' = (Get-Content -LiteralPath (Join-Path $repository 'src/sip-testing-tool/Properties/macOS/Info.plist') -Raw).Replace('__APP_VERSION__', $bundleVersion)
                        'MacOS/sip-testing-tool' = [byte[]]@(0xcf, 0xfa, 0xed, 0xfe, $cpu, 0, 0, 1)
                        'Resources/app.icns' = 'icon fixture'
                        'Resources/sip-testing-tool.dll' = [IO.File]::ReadAllBytes($assemblies[$version])
                        'Resources/System.Private.CoreLib.dll' = 'runtime fixture'
                        'Resources/sip-testing-tool.runtimeconfig.json' = '{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"}]}}'
                        'Frameworks/libcoreclr.dylib' = 'runtime fixture'
                        'Frameworks/libhostfxr.dylib' = 'runtime fixture'
                        'Frameworks/libhostpolicy.dylib' = 'runtime fixture'
                        '_CodeSignature/CodeResources' = 'signature fixture'
                    }
                    if ($scenario -eq 'missing-runtime') { $files.Remove('Resources/System.Private.CoreLib.dll') }
                    if ($scenario -eq 'wrong-application-version') {
                        $different = if ($version -eq '0.0.0-dev') { '3.0.0' } else { '0.0.0-dev' }
                        $files['Resources/sip-testing-tool.dll'] = [IO.File]::ReadAllBytes($assemblies[$different])
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
                    $rejected = $false
                    try {
                        & (Join-Path $PSScriptRoot 'VerifyDesktopRelease.ps1') -Directory $output -RuntimeIdentifier $rid -Version $version
                    } catch {
                        if ($scenario -eq 'valid') { throw }
                        $rejected = $true
                    }
                    if (-not $rejected -and $scenario -ne 'valid') { throw "Accepted invalid macOS release: $rid/$version/$scenario" }
                    if ($scenario -eq 'valid') { Assert-InvalidPackageFiles -ValidDirectory $output -RuntimeIdentifier $rid -Version $version }
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
& (Join-Path $PSScriptRoot 'TestGitHubRelease.ps1')
Write-Host "Verified $($cases.Count) version cases, tag-to-MSBuild injection, development defaults, all release profiles, embedded versions, and single-package outputs."
