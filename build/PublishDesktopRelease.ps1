param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$ExpectedCommit
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
. (Join-Path $PSScriptRoot 'VerifyGitHubRelease.ps1')
$releaseVersion = Get-DesktopReleaseVersion -Version $Tag -Tag
if ($releaseVersion.Version -cne $Version) { throw 'The release version must match its tag.' }
if ($Repository -cnotmatch '\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z') { throw 'Expected a GitHub owner/repository.' }
$prerelease = $releaseVersion.IsPrerelease

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

$existing = Find-DesktopGitHubRelease -Repository $Repository -Tag $Tag
if ($null -ne $existing) {
    if (-not $existing.draft) {
        throw 'This release is already published. Create a new version instead of replacing published assets.'
    }
    $releaseId = $existing.id
} else {
    # REST can create a missing tag. Verify the exact remote ref and peeled commit
    # first, preserving --verify-tag without creating or moving refs ourselves.
    Assert-DesktopGitHubTag -Repository $Repository -Tag $Tag -ExpectedCommit $ExpectedCommit
    $body = @{ tag_name = $Tag; name = "SIP Testing Tool $Tag"; draft = $true; prerelease = $prerelease; generate_release_notes = $true }
    if ($prerelease) { $body.make_latest = 'false' }
    # Never retry an ambiguous POST. A later execution can discover its draft.
    $created = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases" -Method POST -Body $body
    Assert-DesktopGitHubReleaseIdentity -Release $created -Repository $Repository -Tag $Tag -Prerelease $prerelease
    $releaseId = $created.id
}
$null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $releaseId -Prerelease $prerelease
if ($null -ne $existing) { Assert-DesktopGitHubTag -Repository $Repository -Tag $Tag -ExpectedCommit $ExpectedCommit }
# A retry replaces the entire asset set on this draft only, including obsolete
# sidecars or partial uploads. Published releases and tags are never cleaned up.
$staleAssets = @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $releaseId)
foreach ($asset in $staleAssets) {
    if ($asset -isnot [System.Management.Automation.PSCustomObject] -or
        ($asset.id -isnot [long] -and $asset.id -isnot [int]) -or $asset.id -le 0) { throw 'Invalid draft asset ID.' }
}
if (@($staleAssets.id | Sort-Object -Unique).Count -ne $staleAssets.Count) { throw 'Duplicate draft asset ID.' }
foreach ($asset in $staleAssets) {
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $releaseId -Prerelease $prerelease
    $null = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/assets/$($asset.id)" -Method DELETE
}
$uploadedIds = @{}
foreach ($asset in $assets) {
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $releaseId -Prerelease $prerelease
    $name = [Uri]::EscapeDataString($asset.Name)
    $uploaded = Invoke-DesktopGitHubApi -Path "https://uploads.github.com/repos/$Repository/releases/$releaseId/assets?name=$name" -Method POST -InputFile $asset.FullName
    if ($uploaded -isnot [System.Management.Automation.PSCustomObject] -or ($uploaded.id -isnot [long] -and $uploaded.id -isnot [int]) -or
        $uploaded.id -le 0 -or $uploaded.name -isnot [string] -or $uploaded.name -cne $asset.Name -or
        $uploaded.state -isnot [string] -or $uploaded.state -cne 'uploaded' -or $uploaded.id -in @($uploadedIds.Values)) {
        throw 'Invalid GitHub uploaded asset response.'
    }
    $uploadedIds[$asset.Name] = $uploaded.id
}
Assert-DesktopGitHubReleaseDigests -Repository $Repository -Tag $Tag -ReleaseId $releaseId -Prerelease $prerelease -ExpectedDigests $digests -UploadedIds $uploadedIds
Assert-DesktopGitHubTag -Repository $Repository -Tag $Tag -ExpectedCommit $ExpectedCommit
$null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $releaseId -Prerelease $prerelease
# Classification changes only after every digest and the complete asset set pass.
# Like gh release edit, explicitly retain the verified tag on publication.
$body = @{ tag_name = $Tag; draft = $false; prerelease = $prerelease }
if ($prerelease) { $body.make_latest = 'false' }
try {
    $published = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/$releaseId" -Method PATCH -Body $body
    Assert-DesktopGitHubReleaseIdentity -Release $published -Repository $Repository -Tag $Tag -ReleaseId $releaseId -Prerelease $prerelease -Draft $false
} catch {
    # A lost response can follow a successful PATCH. Never retry or attempt to
    # roll back a release that may already be published.
    throw "Could not confirm publication of release ID $releaseId. Check its status on GitHub before rerunning. $($_.Exception.Message)"
}
