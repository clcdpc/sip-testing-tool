# Optional authenticated, read-only qualification against an existing draft.
# Example: ./build/TestGitHubReleaseApi.ps1 -Repository owner/repo -Tag v1.0.0-beta.1 -ReleaseId 123 -ExpectedCommit <40-character SHA>
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][long]$ReleaseId,
    [Parameter(Mandatory)][string]$ExpectedCommit
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseVersion.ps1')
. (Join-Path $PSScriptRoot 'VerifyGitHubRelease.ps1')
if ($Repository -cnotmatch '\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z') { throw 'Expected a GitHub owner/repository.' }
$version = Get-DesktopReleaseVersion -Version $Tag -Tag
$null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $ReleaseId -Prerelease $version.IsPrerelease
Assert-DesktopGitHubTag -Repository $Repository -Tag $Tag -ExpectedCommit $ExpectedCommit
$assets = @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $ReleaseId)
Write-Host "Verified draft $ReleaseId for $Repository $Tag at $ExpectedCommit with $($assets.Count) assets (read-only)."
