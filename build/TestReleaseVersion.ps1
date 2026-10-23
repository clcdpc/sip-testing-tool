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
} finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $scratch.FullName.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $scratch.Name.StartsWith('sip-release-tests-', [StringComparison]::Ordinal)) {
        throw 'The test scratch directory is outside the expected temporary location.'
    }
    $scratch.Delete($true)
}
Write-Host "Verified $($cases.Count) version cases, release tag validation, and stable/beta package names and checksums."
