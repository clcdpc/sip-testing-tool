# Exercise the real publication scripts with fixture bytes and an in-process gh
# transport. Package structure is tested separately by TestReleaseVersion.ps1.
$ErrorActionPreference = 'Stop'
$scratch = [IO.Directory]::CreateTempSubdirectory('sip-github-release-tests-')
$global:SipReleaseTestState = $null

function gh {
    $arguments = @($args)
    $state = $global:SipReleaseTestState
    $state.Calls.Add($arguments)
    $global:LASTEXITCODE = 0
    if ($arguments[0] -eq 'api') {
        $path = $arguments[1]
        $method = $arguments[[array]::IndexOf($arguments, '--method') + 1]
        if ('Accept: application/vnd.github+json' -cnotin $arguments -or 'X-GitHub-Api-Version: 2022-11-28' -cnotin $arguments) {
            throw 'Release metadata must use the GitHub REST JSON API.'
        }
        if ($path -eq 'repos/local/fixture/releases?per_page=100') {
            if ($method -cne 'GET' -or '--paginate' -cnotin $arguments -or '--slurp' -cnotin $arguments) {
                throw 'Discovery must enumerate every release page.'
            }
            $state.ListReads++
            if ($state.Scenario -eq 'list-api-failure' -or
                ($state.Scenario -eq 'list-failure-after-create' -and $state.Created -eq 1)) {
                $global:LASTEXITCODE = 1; return
            }
            if ($state.Scenario -eq 'invalid-list-json') { 'not JSON'; return }
            if ($state.Scenario -eq 'empty-list-response') { return }
            if ($state.Scenario -eq 'null-list-response') { 'null'; return }
            if ($state.Scenario -eq 'invalid-list-pages') { '[{}]'; return }
            if ($state.Scenario -eq 'empty-list-pages') { '[]'; return }
            if ($state.Scenario -eq 'null-list-page') { '[null]'; return }
            # A case-only tag on the first page must not match. The requested
            # release is on a later page; conflicting matches can be later still.
            $first = @(@{ id = 1; tag_name = $state.Tag.ToUpperInvariant(); draft = $false })
            $last = @()
            if ($state.Exists -and $state.Scenario -ne 'not-found-after-create') {
                $release = @{ id = 42; tag_name = $state.Tag; draft = $state.Draft }
                switch ($state.Scenario) {
                    'invalid-list-id' { $release.id = '42' }
                    'zero-list-id' { $release.id = 0 }
                    'negative-list-id' { $release.id = -42 }
                    'fractional-list-id' { $release.id = 42.5 }
                    'invalid-list-draft' { $release.draft = 'true' }
                    'invalid-list-tag' { $release.tag_name = $null }
                }
                $last = @($release)
            }
            if ($state.Scenario -eq 'new-empty-list' -and -not $state.Exists) { $first = @() }
            $pages = @($first, $last)
            if ($state.Scenario -eq 'new-empty-list') { $pages = ,$last }
            if ($state.Scenario -eq 'single-page-draft') { $pages = ,$last }
            switch ($state.Scenario) {
                'ambiguous-drafts' { $pages += ,@(@{ id = 43; tag_name = $state.Tag; draft = $true }) }
                'draft-and-published' { $pages += ,@(@{ id = 43; tag_name = $state.Tag; draft = $false }) }
                'repeated-release-id' { $pages += ,@(@{ id = 42; tag_name = 'v9.9.9'; draft = $true }) }
                'duplicate-release' { $pages += ,@(@{ id = 42; tag_name = $state.Tag; draft = $state.Draft }) }
                'list-partial-failure' { $global:LASTEXITCODE = 1 }
            }
            ConvertTo-Json -InputObject $pages -Depth 8 -Compress
            return
        }
        if ($path.StartsWith('repos/local/fixture/releases/tags/', [StringComparison]::Ordinal)) {
            # GitHub does not resolve a draft through this endpoint. A regression
            # to tag-based discovery must fail even when the draft exists.
            $global:LASTEXITCODE = 1
            return
        }
        if ($method -ceq 'DELETE' -and $path -match '\Arepos/local/fixture/releases/assets/(\d+)\z') {
            $state.Writes.Add("DELETE $path")
            if (-not $state.Draft) { throw 'Attempted to delete a published asset.' }
            $assetId = [long]$Matches[1]
            $asset = @($state.Assets | Where-Object id -eq $assetId)
            if ($asset.Count -ne 1) { throw "Deleted an asset outside this draft: $assetId" }
            if ($state.Scenario -eq 'delete-failure') { $global:LASTEXITCODE = 1; return }
            $null = $state.Assets.Remove($asset[0])
            $state.Deleted.Add($assetId)
            return
        }
        if ($path -ceq 'repos/local/fixture/releases/42' -and $method -ceq 'GET') {
            $state.DirectReads++
            if (-not $state.Exists) { $global:LASTEXITCODE = 1; return }
            if ($state.Scenario -eq 'direct-api-failure' -or
                ($state.Scenario -eq 'cleanup-check-failure' -and $state.DirectReads -eq 2)) {
                $global:LASTEXITCODE = 1; return
            }
            if ($state.Scenario -eq 'direct-published' -or
                ($state.Scenario -eq 'published-before-cleanup' -and $state.DirectReads -eq 2) -or
                ($state.Scenario -eq 'published-during-verification' -and $state.Metadata.Count -eq 4)) {
                $state.Draft = $false
            }
            $tag = if ($state.Scenario -eq 'wrong-tag') { 'v9.9.9' } else { $state.Tag }
            $release = @{ id = 42; tag_name = $tag; draft = $state.Draft }
            if ($state.Scenario -eq 'wrong-release-id') { $release.id = 43 }
            if ($state.Scenario -eq 'invalid-direct-id') { $release.id = '42' }
            if ($state.Scenario -eq 'invalid-direct-draft') { $release.draft = 'true' }
            $release | ConvertTo-Json -Compress
            return
        }
        if ($path -ceq 'repos/local/fixture/releases/42/assets?per_page=100' -and $method -ceq 'GET') {
            if ('--paginate' -cnotin $arguments -or '--slurp' -cnotin $arguments) { throw 'The entire release asset list must be read.' }
            if ($state.Scenario -eq 'asset-list-api-failure') { $global:LASTEXITCODE = 1; return }
            if ($state.Scenario -eq 'invalid-asset-pages') { '[{}]'; return }
            if ($state.Scenario -eq 'changed-asset-set' -and $state.Metadata.Count -eq 4) {
                $state.Assets.Add(@{ id = 900; name = 'late-extra.txt'; state = 'uploaded' })
            }
            # Deliberately paginate and reverse names so matching cannot use order.
            $all = @($state.Assets | Sort-Object name -Descending)
            $first = @($all | Select-Object -First 2)
            $rest = @($all | Select-Object -Skip 2)
            ConvertTo-Json -InputObject @($first, $rest) -Depth 8 -Compress
            return
        }
        if ($method -ceq 'GET' -and $path -match '\Arepos/local/fixture/releases/assets/(\d+)\z') {
            $assetId = [long]$Matches[1]
            $asset = @($state.Assets | Where-Object id -eq $assetId)
            if ($asset.Count -ne 1) { throw "Read an asset outside this draft: $assetId" }
            $state.Metadata.Add($assetId)
            if ($state.Scenario -eq 'api-failure') { $global:LASTEXITCODE = 1; return }
            $metadata = $asset[0].Clone()
            if ($state.Scenario -eq 'wrong-asset-name') { $metadata.name = 'another-package.zip' }
            if ($state.Scenario -eq 'wrong-asset-id') { $metadata.id = 999 }
            if ($state.Scenario -eq 'invalid-asset-id') { $metadata.id = "$assetId" }
            $metadata | ConvertTo-Json -Compress
            return
        }
        throw "Unexpected API request: $method $path"
    }
    if ($arguments[0] -cne 'release' -or $arguments[2] -cne $state.Tag -or 'local/fixture' -cnotin $arguments) {
        throw "Unexpected gh invocation: $arguments"
    }
    # CLI upload/edit support drafts via GraphQL and numeric release URLs. Only
    # the raw REST tag lookup above returns 404; it cannot stand in for these.
    switch ($arguments[1]) {
        'upload' {
            $state.Writes.Add('release upload')
            if (-not $state.Draft -or $state.Assets.Count -ne 0 -or '--clobber' -cin $arguments -or
                $state.Deleted.Count -ne $state.InitialAssetCount) { throw 'Uploads require a clean draft.' }
            $paths = @($arguments | Select-Object -Skip 3 -First 4)
            foreach ($file in $paths) {
                $name = [IO.Path]::GetFileName($file)
                $id = 100 + $state.Assets.Count
                $original = [IO.File]::ReadAllBytes($file)
                if ($state.Scenario -eq 'changed-local-bytes') { [IO.File]::AppendAllText($file, 'changed during upload') }
                $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
                [IO.File]::WriteAllBytes($file, $original)
                if ($state.Scenario -ne 'uppercase-digest') { $hash = $hash.ToLowerInvariant() }
                $state.Assets.Add(@{ id = $id; name = $name; state = 'uploaded'; digest = "sha256:$hash" })
                $state.Uploaded.Add($name)
                if ($state.Scenario -eq 'upload-failure' -and $state.Uploaded.Count -eq 3) { $global:LASTEXITCODE = 1; return }
            }
            $target = $state.Assets[$state.Target]
            switch ($state.Scenario) {
                'mismatch' { $target.digest = 'sha256:' + ('0' * 64) }
                'missing-digest' { $target.Remove('digest') }
                'null-digest' { $target.digest = $null }
                'malformed-digest' { $target.digest = $state.BadDigest }
                'swapped-digests' { $target.digest = $state.Assets[($state.Target + 1) % 4].digest }
                'missing-asset' { $state.Assets.RemoveAt(3) }
                'extra-asset' { $state.Assets.Add(@{ id = 800; name = 'obsolete.sha256'; state = 'uploaded' }) }
                'duplicate-name' { $target.name = $state.Assets[($state.Target + 1) % 4].name }
                'duplicate-id' { $target.id = $state.Assets[($state.Target + 1) % 4].id }
                'unexpected-name' { $target.name = 'unrelated.zip' }
                'unuploaded-asset' { $target.state = 'starter' }
            }
            return
        }
        'edit' {
            $state.Writes.Add('release edit')
            if (-not $state.Draft) { throw 'Attempted to edit a published release.' }
            if ('--prerelease=true' -cnotin $arguments -or '--latest=false' -cnotin $arguments -or '--draft=false' -cnotin $arguments) {
                throw 'Prerelease classification changed.'
            }
            if ($state.Assets.Count -ne 4 -or $state.Metadata.Count -ne 4 -or $state.Uploaded.Count -ne 4) {
                throw 'Published before all four uploaded digests were checked.'
            }
            if ($state.Scenario -eq 'publication-failure') { $global:LASTEXITCODE = 1; return }
            $state.Published++
            $state.Draft = $false
        }
        'create' {
            $state.Writes.Add('release create')
            if ($state.Exists -or '--verify-tag' -cnotin $arguments -or '--draft' -cnotin $arguments -or
                '--prerelease=true' -cnotin $arguments -or '--latest=false' -cnotin $arguments -or '--generate-notes' -cnotin $arguments) {
                throw 'Unsafe draft creation.'
            }
            if ($state.Scenario -eq 'create-failure') { $global:LASTEXITCODE = 1; return }
            $state.Exists = $true
            $state.Created++
            if ($state.Scenario -eq 'create-failed-after-creation') { $global:LASTEXITCODE = 1 }
        }
        default { throw "Unexpected release command: $arguments" }
    }
}

try {
    $directory = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName 'packages')).FullName
    $version = '3.0.0-beta.8+build.001'
    foreach ($rid in @('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')) {
        $extension = if ($rid -eq 'linux-x64') { 'AppImage' } else { 'zip' }
        [IO.File]::WriteAllText((Join-Path $directory "SIP-Testing-Tool_${rid}_v$version.$extension"), "Different fixture bytes for $rid")
    }
    $cases = @(
        @{ Scenario = 'valid' }, @{ Scenario = 'single-page-draft' }, @{ Scenario = 'new-draft' }, @{ Scenario = 'new-empty-list' },
        @{ Scenario = 'retry' }, @{ Scenario = 'partial-upload' }, @{ Scenario = 'sidecars' }, @{ Scenario = 'uppercase-digest' },
        @{ Scenario = 'published'; Error = '*already published*'; NoWrites = $true },
        @{ Scenario = 'wrong-tag'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'wrong-release-id'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'invalid-direct-id'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'invalid-direct-draft'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'direct-published'; Error = '*already published*'; NoWrites = $true },
        @{ Scenario = 'published-before-cleanup'; Error = '*already published*'; NoWrites = $true },
        @{ Scenario = 'published-during-verification'; Error = '*already published*' },
        @{ Scenario = 'invalid-list-id'; Error = '*Invalid GitHub release metadata*'; NoWrites = $true },
        @{ Scenario = 'zero-list-id'; Error = '*Invalid GitHub release metadata*'; NoWrites = $true },
        @{ Scenario = 'negative-list-id'; Error = '*Invalid GitHub release metadata*'; NoWrites = $true },
        @{ Scenario = 'fractional-list-id'; Error = '*Invalid GitHub release metadata*'; NoWrites = $true },
        @{ Scenario = 'invalid-list-draft'; Error = '*Invalid GitHub release metadata*'; NoWrites = $true },
        @{ Scenario = 'invalid-list-tag'; Error = '*Invalid GitHub release metadata*'; NoWrites = $true },
        @{ Scenario = 'ambiguous-drafts'; Error = '*Ambiguous GitHub releases*'; NoWrites = $true },
        @{ Scenario = 'draft-and-published'; Error = '*Ambiguous GitHub releases*'; NoWrites = $true },
        @{ Scenario = 'repeated-release-id'; Error = '*repeated release ID*'; NoWrites = $true },
        @{ Scenario = 'duplicate-release'; Error = '*repeated release ID*'; NoWrites = $true },
        @{ Scenario = 'list-api-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'list-partial-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'invalid-list-json'; Error = '*JSON*'; NoWrites = $true },
        @{ Scenario = 'empty-list-response'; Error = '*Missing GitHub API response*'; NoWrites = $true },
        @{ Scenario = 'null-list-response'; Error = '*Missing GitHub API response*'; NoWrites = $true },
        @{ Scenario = 'invalid-list-pages'; Error = '*Invalid paginated GitHub release list*'; NoWrites = $true },
        @{ Scenario = 'empty-list-pages'; Error = '*Invalid paginated GitHub release list*'; NoWrites = $true },
        @{ Scenario = 'null-list-page'; Error = '*Invalid paginated GitHub release list*'; NoWrites = $true },
        @{ Scenario = 'not-found-after-create'; Error = '*draft was not found*' },
        @{ Scenario = 'list-failure-after-create'; Error = '*GitHub API failed*' },
        @{ Scenario = 'create-failure'; Error = '*Could not create the draft*' },
        @{ Scenario = 'create-failed-after-creation'; Error = '*Could not create the draft*' },
        @{ Scenario = 'direct-api-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'cleanup-check-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'asset-list-api-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'invalid-asset-pages'; Error = '*Invalid paginated GitHub asset list*'; NoWrites = $true },
        @{ Scenario = 'delete-failure'; Error = '*GitHub API failed*' },
        @{ Scenario = 'missing-asset'; Error = '*exactly the four expected*' },
        @{ Scenario = 'extra-asset'; Error = '*exactly the four expected*' },
        @{ Scenario = 'duplicate-name'; Error = '*exactly the four expected*' },
        @{ Scenario = 'duplicate-id'; Error = '*exactly the four expected*' },
        @{ Scenario = 'unexpected-name'; Error = '*Unexpected release asset*' },
        @{ Scenario = 'changed-local-bytes'; Error = '*SHA-256 mismatch*' },
        @{ Scenario = 'wrong-asset-name'; Error = '*metadata does not match*' },
        @{ Scenario = 'wrong-asset-id'; Error = '*metadata does not match*' },
        @{ Scenario = 'invalid-asset-id'; Error = '*metadata does not match*' },
        @{ Scenario = 'unuploaded-asset'; Error = '*Unexpected release asset*' },
        @{ Scenario = 'api-failure'; Error = '*GitHub API failed*' },
        @{ Scenario = 'upload-failure'; Error = '*Could not upload all release assets*' },
        @{ Scenario = 'changed-asset-set'; Error = '*asset set changed*' },
        @{ Scenario = 'publication-failure'; Error = '*Could not publish the release*' }
    )
    foreach ($target in 0..3) {
        foreach ($scenario in @('mismatch', 'missing-digest', 'null-digest', 'swapped-digests')) {
            $expectedError = if ($scenario -in @('mismatch', 'swapped-digests')) { '*SHA-256 mismatch*' } else { '*Missing or malformed*' }
            $cases += @{ Scenario = $scenario; Target = $target; Error = $expectedError }
        }
    }
    foreach ($digest in @('', 'sha256:abc', ('sha512:' + ('a' * 64)), ('sha256:' + ('g' * 64)), ('sha256:' + ('a' * 63)), ('sha256:' + ('a' * 65)), ('sha256:' + ('a' * 64) + "`n"))) {
        $cases += @{ Scenario = 'malformed-digest'; BadDigest = $digest; Error = '*Missing or malformed*' }
    }
    foreach ($case in $cases) {
        $global:SipReleaseTestState = [pscustomobject]@{
            Scenario = $case.Scenario; Target = [int]$case.Target; BadDigest = $case.BadDigest
            Tag = "v$version"
            Exists = $case.Scenario -notin @('new-draft', 'new-empty-list', 'not-found-after-create', 'list-failure-after-create', 'create-failure', 'create-failed-after-creation')
            Draft = $case.Scenario -ne 'published'
            Assets = [Collections.Generic.List[object]]::new(); Calls = [Collections.Generic.List[object]]::new()
            Writes = [Collections.Generic.List[string]]::new(); Uploaded = [Collections.Generic.List[string]]::new()
            Metadata = [Collections.Generic.List[long]]::new(); Deleted = [Collections.Generic.List[long]]::new()
            Published = 0; Created = 0; ListReads = 0; DirectReads = 0; InitialAssetCount = 0
        }
        $state = $global:SipReleaseTestState
        if ($case.Scenario -in @('retry', 'published', 'published-before-cleanup', 'cleanup-check-failure', 'delete-failure')) {
            foreach ($name in @('obsolete-package.zip', 'obsolete-package.zip.sha256', 'unrelated.txt')) {
                $state.Assets.Add(@{ id = 500 + $state.Assets.Count; name = $name; state = 'uploaded' })
            }
        }
        if ($case.Scenario -eq 'partial-upload') {
            $state.Assets.Add(@{ id = 500; name = 'partial.zip'; state = 'starter' })
            $state.Assets.Add(@{ id = 501; name = @(Get-ChildItem -LiteralPath $directory -File)[0].Name; state = 'uploaded' })
        }
        if ($case.Scenario -eq 'sidecars') {
            foreach ($file in Get-ChildItem -LiteralPath $directory -File) {
                $state.Assets.Add(@{ id = 500 + $state.Assets.Count; name = $file.Name + '.sha256'; state = 'uploaded' })
            }
        }
        $state.InitialAssetCount = $state.Assets.Count
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'PublishDesktopRelease.ps1') -Directory $directory -Repository local/fixture -Tag "v$version" -Version $version 6>$null
        } catch {
            if (-not $case.Error -or $_.Exception.Message -notlike $case.Error) { throw "Unexpected failure for $($case.Scenario): $_`n$($_.ScriptStackTrace)" }
            $rejected = $true
        }
        if ($rejected -ne [bool]$case.Error) { throw "Unexpected publication result for $($case.Scenario)" }
        $state = $global:SipReleaseTestState
        if ($case.Error) {
            if ($state.Published -ne 0) { throw "Published despite $($case.Scenario)" }
            if ($case.Scenario -notin @('published', 'direct-published', 'published-before-cleanup', 'published-during-verification') -and -not $state.Draft) {
                throw 'A failed publication must leave a draft.'
            }
            if ($case.NoWrites -and $state.Writes.Count -ne 0) { throw "Unsafe mutation for $($case.Scenario)" }
            if ($case.Scenario -ne 'publication-failure' -and @($state.Writes | Where-Object { $_ -ceq 'release edit' }).Count -ne 0) {
                throw "Attempted publication despite $($case.Scenario)"
            }
        } elseif ($state.Published -ne 1 -or $state.Assets.Count -ne 4 -or @($state.Metadata | Sort-Object -Unique).Count -ne 4) {
            throw "Successful publication did not check all four asset IDs: $($case.Scenario)"
        }
        if (-not $case.Error) {
            if ($state.Deleted.Count -ne $state.InitialAssetCount) { throw 'Draft retry did not remove every stale asset.' }
            $expectedCreates = if ($case.Scenario -in @('new-draft', 'new-empty-list')) { 1 } else { 0 }
            if ($state.Created -ne $expectedCreates -or $state.ListReads -ne (1 + $expectedCreates)) {
                throw 'Creation must follow successful discovery and rediscover the new draft.'
            }
        }
        if ($case.Scenario -eq 'upload-failure' -and $state.Assets.Count -ne 3) { throw 'Upload failure did not leave a resumable partial draft.' }
    }
    # Invalid local sets must fail before looking up or modifying any release.
    foreach ($scenario in @('missing', 'extra', 'wrong-name', 'empty')) {
        $local = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName $scenario)).FullName
        foreach ($file in Get-ChildItem -LiteralPath $directory -File) { [IO.File]::Copy($file.FullName, (Join-Path $local $file.Name)) }
        $first = @(Get-ChildItem -LiteralPath $local -File)[0].FullName
        switch ($scenario) {
            'missing' { [IO.File]::Delete($first) }
            'extra' { [IO.File]::WriteAllText((Join-Path $local 'stale.sha256'), 'obsolete') }
            'wrong-name' { [IO.File]::Move($first, (Join-Path $local 'wrong.zip')) }
            'empty' { [IO.File]::WriteAllText($first, '') }
        }
        $global:SipReleaseTestState.Calls.Clear()
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'PublishDesktopRelease.ps1') -Directory $local -Repository local/fixture -Tag "v$version" -Version $version 6>$null
        } catch {
            if ($_.Exception.Message -notlike '*local release package*') { throw }
            $rejected = $true
        }
        if (-not $rejected -or $global:SipReleaseTestState.Calls.Count -ne 0) { throw "Invalid local packages reached GitHub: $scenario" }
    }
    Write-Host "Verified $($cases.Count) offline GitHub publication cases and four invalid local asset sets."
} finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $scratch.FullName.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $scratch.Name.StartsWith('sip-github-release-tests-', [StringComparison]::Ordinal)) {
        throw 'The GitHub test scratch directory is outside the expected temporary location.'
    }
    $scratch.Delete($true)
    Remove-Variable -Name SipReleaseTestState -Scope Global
}
