# Shared by publication and the offline release tests. All metadata comes from the
# authenticated REST API, not the GitHub Actions artifact archive's digest.
function Invoke-DesktopGitHubApi {
    param([string]$Path, [string]$Method = 'GET', [switch]$Paginate)
    $arguments = @('api', $Path, '--method', $Method, '-H', 'Accept: application/vnd.github+json', '-H', 'X-GitHub-Api-Version: 2022-11-28')
    if ($Paginate) { $arguments += @('--paginate', '--slurp') }
    $response = & gh @arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub API failed: $Method $Path" }
    if ($Method -ne 'DELETE') {
        $parsed = $response | ConvertFrom-Json -NoEnumerate
        if ($null -eq $parsed) { throw "Missing GitHub API response: $Method $Path" }
        # Preserve empty and single-page arrays so malformed pagination is not
        # confused with a successful list containing no releases or assets.
        return ,$parsed
    }
}

function Find-DesktopGitHubRelease {
    param([string]$Repository, [string]$Tag)
    # The tag endpoint only finds published releases. Authenticated enumeration
    # includes drafts, and an API failure must never mean "safe to create".
    $pages = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases?per_page=100" -Paginate
    if ($pages -isnot [array] -or $pages.Count -eq 0) { throw 'Invalid paginated GitHub release list.' }
    $seenIds = @{}
    $matching = @(foreach ($page in $pages) {
        if ($page -isnot [array]) { throw 'Invalid paginated GitHub release list.' }
        foreach ($release in $page) {
            if (($release.id -isnot [long] -and $release.id -isnot [int]) -or $release.id -le 0 -or
                $release.tag_name -isnot [string] -or [string]::IsNullOrWhiteSpace($release.tag_name) -or
                $release.draft -isnot [bool]) {
                throw 'Invalid GitHub release metadata in the release list.'
            }
            if ($seenIds.ContainsKey($release.id)) { throw 'Ambiguous GitHub release list: repeated release ID.' }
            $seenIds[$release.id] = $true
            if ($release.tag_name -ceq $Tag) { $release }
        }
    })
    if ($matching.Count -gt 1) { throw 'Ambiguous GitHub releases for the requested tag.' }
    if ($matching.Count -eq 1) { $matching[0] }
}

function Get-DesktopDraftRelease {
    param([string]$Repository, [string]$Tag, [long]$ReleaseId = 0)
    if ($ReleaseId -eq 0) {
        $discovered = Find-DesktopGitHubRelease -Repository $Repository -Tag $Tag
        if ($null -eq $discovered) { throw 'The intended draft was not found in the GitHub release list.' }
        if (-not $discovered.draft) { throw 'This release is already published. Create a new version instead of replacing published assets.' }
        $ReleaseId = $discovered.id
    }
    if ($ReleaseId -le 0) { throw 'Invalid GitHub release ID.' }
    $release = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/$ReleaseId"
    if (($release.id -isnot [long] -and $release.id -isnot [int]) -or $release.id -le 0 -or
        $release.id -ne $ReleaseId -or $release.tag_name -cne $Tag -or $release.draft -isnot [bool]) {
        throw 'The GitHub release does not match the intended release ID and tag.'
    }
    if (-not $release.draft) {
        throw 'This release is already published. Create a new version instead of replacing published assets.'
    }
    $release
}

function Get-DesktopGitHubReleaseAssets {
    param([string]$Repository, [long]$ReleaseId)
    $pages = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/$ReleaseId/assets?per_page=100" -Paginate
    if ($pages -isnot [array] -or $pages.Count -eq 0) { throw 'Invalid paginated GitHub asset list.' }
    foreach ($page in $pages) {
        if ($page -isnot [array]) { throw 'Invalid paginated GitHub asset list.' }
        foreach ($asset in $page) { $asset }
    }
}

function Assert-DesktopGitHubReleaseDigests {
    param([string]$Repository, [string]$Tag, [long]$ReleaseId, [hashtable]$ExpectedDigests)
    if ($ExpectedDigests.Count -ne 4) { throw 'Expected exactly four local release packages.' }
    foreach ($hash in $ExpectedDigests.Values) {
        if ($hash -isnot [string] -or $hash -cnotmatch '\A[0-9a-fA-F]{64}\z') { throw 'Invalid local SHA-256.' }
    }
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $ReleaseId
    $assets = @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $ReleaseId)
    if ($assets.Count -ne 4 -or @($assets.name | Sort-Object -Unique).Count -ne 4 -or
        @($assets.id | Sort-Object -Unique).Count -ne 4) {
        throw 'The draft must contain exactly the four expected release assets.'
    }
    foreach ($asset in $assets) {
        if ($asset.name -cnotin @($ExpectedDigests.Keys) -or $asset.state -cne 'uploaded' -or
            ($asset.id -isnot [long] -and $asset.id -isnot [int]) -or $asset.id -le 0) {
            throw "Unexpected release asset: $($asset.name)"
        }
        # Resolve by the ID listed on this draft, then require the same ID and name.
        $metadata = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/assets/$($asset.id)"
        if (($metadata.id -isnot [long] -and $metadata.id -isnot [int]) -or
            $metadata.id -ne $asset.id -or $metadata.name -cne $asset.name -or $metadata.state -cne 'uploaded') {
            throw "Release asset metadata does not match: $($asset.name)"
        }
        if ($metadata.digest -isnot [string] -or $metadata.digest -cnotmatch '\Asha256:([0-9a-fA-F]{64})\z') {
            throw "Missing or malformed GitHub SHA-256 digest: $($asset.name)"
        }
        if (-not [string]::Equals($Matches[1], $ExpectedDigests[$asset.name], [StringComparison]::OrdinalIgnoreCase)) {
            throw "GitHub SHA-256 mismatch: $($asset.name)"
        }
        Write-Host "Verified GitHub SHA-256: $($asset.name) (asset $($asset.id))"
    }
    # Recheck the draft and its complete asset set immediately before publication.
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $ReleaseId
    $finalAssets = @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $ReleaseId)
    if ($finalAssets.Count -ne 4) { throw 'The draft asset set changed during verification.' }
    foreach ($asset in $assets) {
        $same = @($finalAssets | Where-Object { $_.id -eq $asset.id -and $_.name -ceq $asset.name -and $_.state -ceq 'uploaded' })
        if ($same.Count -ne 1) { throw 'The draft asset set changed during verification.' }
    }
}
