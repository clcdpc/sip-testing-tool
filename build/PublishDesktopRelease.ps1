param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
. (Join-Path $PSScriptRoot 'VerifyGitHubRelease.ps1')
$releaseVersion = Get-DesktopReleaseVersion -Version $Tag -Tag
if ($releaseVersion.Version -cne $Version) { throw 'The release version must match its tag.' }
$releaseOptions = @('--prerelease=' + $releaseVersion.IsPrerelease.ToString().ToLowerInvariant())
if ($releaseVersion.IsPrerelease) { $releaseOptions += '--latest=false' }

# The workflow runs VerifyDesktopRelease.ps1 on each downloaded package first.
$expectedNames = @(foreach ($rid in @('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')) {
    $extension = if ($rid -eq 'linux-x64') { 'AppImage' } else { 'zip' }
    "SIP-Testing-Tool_${rid}_v$Version.$extension"
})
$assets = @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Force)
if ($assets.Count -ne 4 -or @($assets.Name | Sort-Object -Unique).Count -ne 4) {
    throw 'Expected exactly four uniquely named local release packages.'
}
foreach ($asset in $assets) {
    if ($asset.Name -cnotin $expectedNames -or $asset.Length -eq 0) { throw "Unexpected local release package: $($asset.Name)" }
}
# Capture the exact local bytes before any upload, independently for every package.
$digests = @{}
foreach ($asset in $assets) { $digests[$asset.Name] = (Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256).Hash }

$existing = & gh release view $Tag --repo $Repository --json isDraft 2>$null
if ($LASTEXITCODE -eq 0) {
    if (($existing | ConvertFrom-Json).isDraft -ne $true) {
        throw 'This release is already published. Create a new version instead of replacing published assets.'
    }
    $draft = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag
    & gh release edit $Tag --repo $Repository --draft @releaseOptions
    if ($LASTEXITCODE -ne 0) { throw 'Could not update the draft release classification.' }
} else {
    & gh release create $Tag --repo $Repository --verify-tag --draft --title "SIP Testing Tool $Tag" --generate-notes @releaseOptions
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the draft release.' }
    $draft = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag
}
# A retry replaces the entire asset set on this draft only, including obsolete
# sidecars or partial uploads. Published releases and tags are never cleaned up.
foreach ($asset in @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $draft.id)) {
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $draft.id
    if (($asset.id -isnot [long] -and $asset.id -isnot [int]) -or $asset.id -le 0) { throw 'Invalid draft asset ID.' }
    $null = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/assets/$($asset.id)" -Method DELETE
}
$null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $draft.id
$assetPaths = @($assets.FullName)
& gh release upload $Tag @assetPaths --repo $Repository
if ($LASTEXITCODE -ne 0) { throw 'Could not upload all release assets. The release remains a draft.' }
Assert-DesktopGitHubReleaseDigests -Repository $Repository -Tag $Tag -ReleaseId $draft.id -ExpectedDigests $digests
& gh release edit $Tag --repo $Repository --draft=false @releaseOptions
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the release. Check its status on GitHub before rerunning.' }
