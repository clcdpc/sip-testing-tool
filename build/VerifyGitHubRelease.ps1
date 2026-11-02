# Shared by publication and the offline release tests. All metadata comes from the
# authenticated REST API, not the GitHub Actions artifact archive's digest.
function Invoke-DesktopGitHubApi {
    param([string]$Path, [string]$Method = 'GET', [switch]$Paginate, [hashtable]$Body, [string]$InputFile)
    $arguments = @('api', $Path, '--hostname', 'github.com', '--method', $Method, '-H', 'Accept: application/vnd.github+json', '-H', 'X-GitHub-Api-Version: 2022-11-28')
    if ($Paginate) { $arguments += @('--paginate', '--slurp') }
    if ($null -ne $Body -and $InputFile) { throw 'A GitHub request cannot have both JSON and file bodies.' }
    if ($null -ne $Body) {
        # --input preserves JSON booleans and literal tag/version strings.
        $response = ConvertTo-Json -InputObject $Body -Depth 10 -Compress | & gh @arguments --input - -H 'Content-Type: application/json; charset=utf-8'
    } elseif ($InputFile) {
        # gh streams the file bytes; PowerShell must never decode binary packages.
        $response = & gh @arguments --input $InputFile -H 'Content-Type: application/octet-stream'
    } else {
        $response = & gh @arguments
    }
    if ($LASTEXITCODE -ne 0) { throw "GitHub API failed: $Method $Path" }
    if ($Method -ne 'DELETE') {
        $parsed = ($response -join "`n") | ConvertFrom-Json -NoEnumerate
        if ($null -eq $parsed) { throw "Missing GitHub API response: $Method $Path" }
        # Preserve empty and single-page arrays so malformed pagination is not
        # confused with a successful list containing no releases or assets.
        return ,$parsed
    }
}

function Assert-DesktopGitHubTag {
    param([string]$Repository, [string]$Tag, [string]$ExpectedCommit)
    if ($ExpectedCommit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid expected release commit.' }
    $encodedTag = [Uri]::EscapeDataString($Tag)
    $reference = Invoke-DesktopGitHubApi -Path "repos/$Repository/git/ref/tags/$encodedTag"
    if ($reference -isnot [System.Management.Automation.PSCustomObject] -or $reference.ref -isnot [string] -or $reference.ref -cne "refs/tags/$Tag") {
        throw 'The GitHub tag reference does not match the requested release tag.'
    }
    $target = $reference.object
    $seen = @{}
    # Lightweight tags point straight to a commit; annotated tags can be nested.
    while ($true) {
        if ($target -isnot [System.Management.Automation.PSCustomObject] -or $target.sha -isnot [string] -or
            $target.sha -cnotmatch '\A[0-9a-f]{40}\z' -or $target.type -isnot [string] -or $target.type -cnotin @('tag', 'commit')) {
            throw 'Invalid GitHub release tag target.'
        }
        if ($seen.ContainsKey($target.sha)) { throw 'Cyclic GitHub release tag target.' }
        $seen[$target.sha] = $true
        if ($target.type -ceq 'commit') { break }
        $annotation = Invoke-DesktopGitHubApi -Path "repos/$Repository/git/tags/$($target.sha)"
        if ($annotation -isnot [System.Management.Automation.PSCustomObject] -or $annotation.sha -isnot [string] -or $annotation.sha -cne $target.sha) {
            throw 'The annotated GitHub tag does not match its requested object.'
        }
        $target = $annotation.object
    }
    if ($target.sha -cne $ExpectedCommit) { throw 'The release tag does not point to the expected commit.' }
}

function Assert-DesktopGitHubReleaseIdentity {
    param($Release, [string]$Repository, [string]$Tag, [long]$ReleaseId, [bool]$Prerelease, [bool]$Draft = $true)
    if ($Release -isnot [System.Management.Automation.PSCustomObject] -or
        ($Release.id -isnot [long] -and $Release.id -isnot [int]) -or $Release.id -le 0 -or
        ($ReleaseId -gt 0 -and $Release.id -ne $ReleaseId) -or
        $Release.tag_name -isnot [string] -or $Release.tag_name -cne $Tag -or $Release.draft -isnot [bool]) {
        throw 'The GitHub release does not match the intended release ID and tag.'
    }
    if ($Release.url -isnot [string] -or -not [string]::Equals($Release.url, "https://api.github.com/repos/$Repository/releases/$($Release.id)", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The GitHub release does not belong to the intended repository.'
    }
    if ($Draft -and -not $Release.draft) {
        throw 'This release is already published. Create a new version instead of replacing published assets.'
    }
    if ($Release.draft -ne $Draft -or $Release.prerelease -isnot [bool] -or $Release.prerelease -ne $Prerelease) {
        throw 'The GitHub release classification does not match the intended draft and prerelease status.'
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
            if ($release -isnot [System.Management.Automation.PSCustomObject] -or
                ($release.id -isnot [long] -and $release.id -isnot [int]) -or $release.id -le 0 -or
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
    param([string]$Repository, [string]$Tag, [Parameter(Mandatory)][long]$ReleaseId, [bool]$Prerelease)
    if ($ReleaseId -le 0) { throw 'Invalid GitHub release ID.' }
    $release = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/$ReleaseId"
    Assert-DesktopGitHubReleaseIdentity -Release $release -Repository $Repository -Tag $Tag -ReleaseId $ReleaseId -Prerelease $Prerelease
    $release
}

function Get-DesktopGitHubReleaseAssets {
    param([string]$Repository, [long]$ReleaseId)
    $pages = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/$ReleaseId/assets?per_page=100" -Paginate
    if ($pages -isnot [array] -or $pages.Count -eq 0) { throw 'Invalid paginated GitHub asset list.' }
    foreach ($page in $pages) {
        if ($page -isnot [array]) { throw 'Invalid paginated GitHub asset list.' }
        foreach ($asset in $page) {
            if ($asset -isnot [System.Management.Automation.PSCustomObject]) { throw 'Invalid GitHub asset metadata.' }
            $asset
        }
    }
}

function Assert-DesktopGitHubReleaseDigests {
    param([string]$Repository, [string]$Tag, [long]$ReleaseId, [bool]$Prerelease, [hashtable]$ExpectedDigests, [hashtable]$UploadedIds)
    if ($ExpectedDigests.Count -ne 4) { throw 'Expected exactly four local release packages.' }
    if ($UploadedIds.Count -ne 4) { throw 'Expected exactly four uploaded asset IDs.' }
    foreach ($hash in $ExpectedDigests.Values) {
        if ($hash -isnot [string] -or $hash -cnotmatch '\A[0-9a-fA-F]{64}\z') { throw 'Invalid local SHA-256.' }
    }
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $ReleaseId -Prerelease $Prerelease
    $assets = @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $ReleaseId)
    if ($assets.Count -ne 4 -or @($assets.name | Sort-Object -Unique).Count -ne 4 -or
        @($assets.id | Sort-Object -Unique).Count -ne 4) {
        throw 'The draft must contain exactly the four expected release assets.'
    }
    foreach ($asset in $assets) {
        if ($asset.name -isnot [string] -or $asset.name -cnotin @($ExpectedDigests.Keys) -or
            $asset.state -isnot [string] -or $asset.state -cne 'uploaded' -or
            ($asset.id -isnot [long] -and $asset.id -isnot [int]) -or $asset.id -le 0) {
            throw "Unexpected release asset: $($asset.name)"
        }
        if ($asset.id -ne $UploadedIds[$asset.name]) { throw 'The draft asset set does not match the uploaded asset IDs.' }
        # Resolve by the ID listed on this draft, then require the same ID and name.
        $metadata = Invoke-DesktopGitHubApi -Path "repos/$Repository/releases/assets/$($asset.id)"
        if ($metadata -isnot [System.Management.Automation.PSCustomObject] -or ($metadata.id -isnot [long] -and $metadata.id -isnot [int]) -or
            $metadata.id -ne $asset.id -or $metadata.name -isnot [string] -or $metadata.name -cne $asset.name -or
            $metadata.state -isnot [string] -or $metadata.state -cne 'uploaded') {
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
    $null = Get-DesktopDraftRelease -Repository $Repository -Tag $Tag -ReleaseId $ReleaseId -Prerelease $Prerelease
    $finalAssets = @(Get-DesktopGitHubReleaseAssets -Repository $Repository -ReleaseId $ReleaseId)
    if ($finalAssets.Count -ne 4) { throw 'The draft asset set changed during verification.' }
    foreach ($asset in $assets) {
        $same = @($finalAssets | Where-Object {
            ($_.id -is [long] -or $_.id -is [int]) -and $_.id -eq $asset.id -and
            $_.name -is [string] -and $_.name -ceq $asset.name -and $_.state -is [string] -and $_.state -ceq 'uploaded' -and
            $_.digest -is [string] -and $_.digest -cmatch '\Asha256:[0-9a-fA-F]{64}\z' -and
            [string]::Equals($_.digest.Substring(7), $ExpectedDigests[$asset.name], [StringComparison]::OrdinalIgnoreCase)
        })
        if ($same.Count -ne 1) { throw 'The draft asset set changed during verification.' }
    }
}
