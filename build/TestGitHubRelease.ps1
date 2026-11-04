# Exercise the real publication scripts with fixture bytes and an in-process gh
# transport. Package structure is tested separately by TestReleaseVersion.ps1.
$ErrorActionPreference = 'Stop'
$scratch = [IO.Directory]::CreateTempSubdirectory('sip-github-release-tests-')
$global:SipReleaseTestState = $null

function Get-FixtureRelease {
    $state = $global:SipReleaseTestState
    @{
        id = $state.ReleaseId; tag_name = $state.Tag; draft = $state.Draft; prerelease = $state.Prerelease
        url = "https://api.github.com/repos/local/fixture/releases/$($state.ReleaseId)"
        assets_url = "https://api.github.com/repos/local/fixture/releases/$($state.ReleaseId)/assets"
        upload_url = "https://uploads.github.com/repos/local/fixture/releases/$($state.ReleaseId)/assets{?name,label}"
        name = "SIP Testing Tool $($state.Tag)"; body = 'Generated release notes'; target_commitish = 'master'
    }
}

function gh {
    $arguments = @($args)
    $requestBody = @($input) -join "`n"
    $state = $global:SipReleaseTestState
    $state.Calls.Add($arguments)
    $global:LASTEXITCODE = 0
    # Upload/edit subcommands also rediscover by tag, so reject them outright.
    if ($arguments[0] -cne 'api') { throw "Unexpected gh invocation: $arguments" }
    $path = $arguments[1]
    $method = $arguments[[array]::IndexOf($arguments, '--method') + 1]
    if ('Accept: application/vnd.github+json' -cnotin $arguments -or 'X-GitHub-Api-Version: 2022-11-28' -cnotin $arguments -or
        $arguments[[array]::IndexOf($arguments, '--hostname') + 1] -cne 'github.com') {
        throw 'Release metadata must use the GitHub REST JSON API.'
    }
    if ($path -ceq 'repos/local/fixture/releases?per_page=100') {
        if ($method -cne 'GET' -or '--paginate' -cnotin $arguments -or '--slurp' -cnotin $arguments) { throw 'Discovery must enumerate every release page.' }
        $state.ListReads++
        if ($state.Scenario -eq 'list-api-failure' -or ($state.Scenario -eq 'list-failure-after-create' -and $state.Created -eq 1)) { $global:LASTEXITCODE = 1; return }
        if ($state.Scenario -eq 'invalid-list-json') { 'not JSON'; return }
        if ($state.Scenario -eq 'empty-list-response') { return }
        if ($state.Scenario -eq 'null-list-response') { 'null'; return }
        if ($state.Scenario -eq 'invalid-list-pages') { '[{}]'; return }
        if ($state.Scenario -eq 'empty-list-pages') { '[]'; return }
        if ($state.Scenario -eq 'null-list-page') { '[null]'; return }
        # A case-only tag must not match. Newly created drafts stay invisible
        # to enumeration in every scenario, even though direct ID lookup works.
        $first = @(@{ id = 1; tag_name = $state.Tag.ToUpperInvariant(); draft = $false })
        $last = @()
        if ($state.Exists -and -not $state.New) {
            $release = Get-FixtureRelease
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
        if ($state.Scenario -eq 'new-empty-list') { $first = @() }
        $pages = @($first, $last)
        if ($state.Scenario -in @('new-empty-list', 'single-page-draft')) { $pages = ,$last }
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
    if ($path -ceq "repos/local/fixture/git/ref/tags/$([Uri]::EscapeDataString($state.Tag))" -and $method -ceq 'GET') {
        $state.TagReads++
        if ($state.Scenario -eq 'missing-tag') { $global:LASTEXITCODE = 1; return }
        $reference = @{ ref = "refs/tags/$($state.Tag)"; object = @{ type = 'commit'; sha = $state.Commit } }
        switch ($state.Scenario) {
            'wrong-tag-ref' { $reference.ref = 'refs/tags/v9.9.9' }
            'invalid-tag-sha' { $reference.object.sha = 'bad' }
            'invalid-tag-type' { $reference.object.type = 'tree' }
            'wrong-tag-commit' { $reference.object.sha = 'd' * 40 }
            'changed-tag-commit' { if ($state.TagReads -gt 1) { $reference.object.sha = 'd' * 40 } }
        }
        if ($state.Scenario -in @('annotated-tag', 'nested-tag', 'cyclic-tag', 'wrong-annotation')) { $reference.object = @{ type = 'tag'; sha = 'b' * 40 } }
        $reference | ConvertTo-Json -Depth 5 -Compress
        return
    }
    if ($path -match '\Arepos/local/fixture/git/tags/([bc]{40})\z' -and $method -ceq 'GET') {
        $sha = $Matches[1]
        $annotation = @{ sha = $sha; object = @{ type = 'commit'; sha = $state.Commit } }
        if ($state.Scenario -eq 'nested-tag' -and $sha -ceq ('b' * 40)) { $annotation.object = @{ type = 'tag'; sha = 'c' * 40 } }
        if ($state.Scenario -eq 'cyclic-tag') { $annotation.object = @{ type = 'tag'; sha = 'b' * 40 } }
        if ($state.Scenario -eq 'wrong-annotation') { $annotation.sha = 'd' * 40 }
        $annotation | ConvertTo-Json -Depth 5 -Compress
        return
    }
    if ($path -ceq 'repos/local/fixture/releases' -and $method -ceq 'POST') {
        $state.Writes.Add('POST releases')
        $state.CreateCalls++
        if (-not $state.New -or $state.Exists -or $state.ListReads -ne 1 -or $state.TagReads -ne 1 -or $arguments[[array]::IndexOf($arguments, '--input') + 1] -cne '-') { throw 'Unsafe draft creation.' }
        $body = $requestBody | ConvertFrom-Json
        if ('Content-Type: application/json; charset=utf-8' -cnotin $arguments) { throw 'Creation requires a JSON content type.' }
        if ($body.tag_name -cne $state.Tag -or $body.name -cne "SIP Testing Tool $($state.Tag)" -or
            $body.draft -isnot [bool] -or -not $body.draft -or $body.prerelease -isnot [bool] -or $body.prerelease -ne $state.Prerelease -or
            $body.generate_release_notes -isnot [bool] -or -not $body.generate_release_notes) { throw 'Incorrect REST creation body.' }
        $expectedFields = @('tag_name', 'name', 'draft', 'prerelease', 'generate_release_notes')
        if ($state.Prerelease) {
            $expectedFields += 'make_latest'
            if ($body.make_latest -isnot [string] -or $body.make_latest -cne 'false') { throw 'Prereleases cannot become Latest.' }
        }
        if (@(Compare-Object $expectedFields @($body.psobject.Properties.Name)).Count -ne 0) { throw 'Unexpected release creation fields.' }
        if ($state.Scenario -eq 'create-failure') { $global:LASTEXITCODE = 1; return }
        $state.Exists = $true
        $state.ReleaseId = if ($state.Scenario -eq 'new-large-id') { 4318707900L } else { 314L }
        if ($state.Scenario -eq 'creation-conflict') { $global:LASTEXITCODE = 1; return }
        $state.Created++
        $release = Get-FixtureRelease
        if ($state.Scenario -eq 'create-failed-after-creation') { $global:LASTEXITCODE = 1 }
        switch ($state.Scenario) {
            'created-no-id' { $release.Remove('id') }
            'created-null-id' { $release.id = $null }
            'created-string-id' { $release.id = '314' }
            'created-zero-id' { $release.id = 0 }
            'created-negative-id' { $release.id = -314 }
            'created-fractional-id' { $release.id = 314.5 }
            'created-boolean-id' { $release.id = $true }
            'created-wrong-tag' { $release.tag_name = 'v9.9.9' }
            'created-case-tag' { $release.tag_name = $state.Tag.ToUpperInvariant() }
            'created-published' { $release.draft = $false }
            'created-invalid-draft' { $release.draft = 'true' }
            'created-no-draft' { $release.Remove('draft') }
            'created-wrong-prerelease' { $release.prerelease = -not $state.Prerelease }
            'created-invalid-prerelease' { $release.prerelease = 'true' }
            'created-no-prerelease' { $release.Remove('prerelease') }
            'created-wrong-repository' { $release.url = 'https://api.github.com/repos/another/fixture/releases/314' }
            'created-wrong-url-id' { $release.url = 'https://api.github.com/repos/local/fixture/releases/42' }
            'created-no-url' { $release.Remove('url') }
            'created-invalid-json' { 'not JSON'; return }
            'created-empty-response' { return }
            'created-null-response' { 'null'; return }
            'created-array-response' { ConvertTo-Json -InputObject @($release) -Compress; return }
        }
        $release | ConvertTo-Json -Compress
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
    if ($path -ceq "repos/local/fixture/releases/$($state.ReleaseId)" -and $method -ceq 'GET') {
        $state.DirectReads++
        if (-not $state.Exists) { throw 'Read a nonexistent draft.' }
        if ($state.Scenario -in @('direct-api-failure', 'new-direct-api-failure') -or
            ($state.Scenario -eq 'upload-check-failure' -and $state.Uploaded.Count -eq 2) -or
            ($state.Scenario -eq 'cleanup-check-failure' -and $state.DirectReads -eq 2)) { $global:LASTEXITCODE = 1; return }
        if ($state.Scenario -eq 'direct-published' -or ($state.Scenario -eq 'published-before-cleanup' -and $state.DirectReads -eq 2) -or
            ($state.Scenario -eq 'published-during-verification' -and $state.Metadata.Count -eq 4) -or
            ($state.Scenario -eq 'published-after-final-tag-check' -and $state.TagReads -eq 2)) { $state.Draft = $false }
        $release = Get-FixtureRelease
        switch ($state.Scenario) {
            'wrong-tag' { $release.tag_name = 'v9.9.9' }
            'wrong-release-id' { $release.id = 43 }
            'invalid-direct-id' { $release.id = '42' }
            'invalid-direct-draft' { $release.draft = 'true' }
            'wrong-direct-prerelease' { $release.prerelease = -not $state.Prerelease }
            'wrong-direct-repository' { $release.url = 'https://api.github.com/repos/wrong/repository/releases/42' }
            'direct-array-response' { ConvertTo-Json -InputObject @($release) -Compress; return }
        }
        $release | ConvertTo-Json -Compress
        return
    }
    if ($path -ceq "repos/local/fixture/releases/$($state.ReleaseId)/assets?per_page=100" -and $method -ceq 'GET') {
        if ('--paginate' -cnotin $arguments -or '--slurp' -cnotin $arguments) { throw 'The entire release asset list must be read.' }
        if ($state.Scenario -eq 'asset-list-api-failure' -or
            ($state.Scenario -eq 'asset-list-after-upload-failure' -and $state.Uploaded.Count -eq 4)) { $global:LASTEXITCODE = 1; return }
        if ($state.Scenario -eq 'invalid-asset-pages') { '[{}]'; return }
        if ($state.Scenario -eq 'nested-asset-object') { '[[[{"id":500}]]]'; return }
        if ($state.Metadata.Count -eq 4) {
            switch ($state.Scenario) {
                'changed-asset-set' { $state.Assets.Add(@{ id = 900; name = 'late-extra.txt'; state = 'uploaded' }) }
                'changed-final-id' { $state.Assets[0].id = [string]$state.Assets[0].id }
                'changed-final-name' { $state.Assets[0].name = 'replaced.zip' }
                'changed-final-digest' { $state.Assets[0].digest = 'sha256:' + ('0' * 64) }
                'changed-final-state-type' { $state.Assets[0].state = @('uploaded') }
            }
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
        if ($state.Scenario -eq 'invalid-asset-state') { $metadata.state = @('uploaded') }
        if ($state.Scenario -eq 'invalid-asset-name') { $metadata.name = @($metadata.name) }
        if ($state.Scenario -eq 'asset-array-response') { ConvertTo-Json -InputObject @($metadata) -Compress; return }
        $metadata | ConvertTo-Json -Compress
        return
    }
    if ($method -ceq 'POST' -and $path.StartsWith("https://uploads.github.com/repos/local/fixture/releases/$($state.ReleaseId)/assets?name=", [StringComparison]::Ordinal)) {
        $state.Writes.Add('POST assets')
        if (-not $state.Draft -or $state.Deleted.Count -ne $state.InitialAssetCount -or
            ($state.Uploaded.Count -eq 0 -and $state.Assets.Count -ne 0) -or 'Content-Type: application/octet-stream' -cnotin $arguments) { throw 'Uploads require a clean draft and binary content.' }
        $file = $arguments[[array]::IndexOf($arguments, '--input') + 1]
        $name = [IO.Path]::GetFileName($file)
        if ($file -ceq '-' -or -not $path.EndsWith('?name=' + [Uri]::EscapeDataString($name), [StringComparison]::Ordinal)) { throw 'Upload must stream the exact package with an encoded filename.' }
        $original = [IO.File]::ReadAllBytes($file)
        if ($state.Scenario -eq 'changed-local-bytes') { [IO.File]::AppendAllText($file, 'changed during upload') }
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        [IO.File]::WriteAllBytes($file, $original)
        if ($state.Scenario -ne 'uppercase-digest') { $hash = $hash.ToLowerInvariant() }
        $asset = @{ id = 100 + $state.Uploaded.Count; name = $name; state = 'uploaded'; digest = "sha256:$hash" }
        $state.Assets.Add($asset)
        $state.Uploaded.Add($name)
        if ($state.Scenario -eq 'upload-failure' -and $state.Uploaded.Count -eq 3) { $global:LASTEXITCODE = 1; return }
        $response = $asset.Clone()
        switch ($state.Scenario) {
            'upload-no-id' { $response.Remove('id') }
            'upload-string-id' { $response.id = '100' }
            'upload-zero-id' { $response.id = 0 }
            'upload-wrong-name' { $response.name = 'other.zip' }
            'upload-starter' { $response.state = 'starter' }
            'upload-invalid-state' { $response.state = @('uploaded') }
            'upload-invalid-name' { $response.name = @($name) }
            'upload-duplicate-id' { $response.id = 100 }
            'upload-array-response' { ConvertTo-Json -InputObject @($response) -Compress; return }
        }
        if ($state.Uploaded.Count -eq 4) {
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
                'replaced-upload-id' { $target.id = 999 }
            }
        }
        $response | ConvertTo-Json -Compress
        return
    }
    if ($path -ceq "repos/local/fixture/releases/$($state.ReleaseId)" -and $method -ceq 'PATCH') {
        $state.Writes.Add('PATCH release')
        $state.PublishCalls++
        if (-not $state.Draft) { throw 'Attempted to edit a published release.' }
        $body = $requestBody | ConvertFrom-Json
        $expectedFields = @('tag_name', 'draft', 'prerelease')
        if ($body.tag_name -cne $state.Tag -or $body.draft -isnot [bool] -or $body.draft -or $body.prerelease -isnot [bool] -or $body.prerelease -ne $state.Prerelease -or
            'Content-Type: application/json; charset=utf-8' -cnotin $arguments) { throw 'Incorrect REST publication body.' }
        if ($state.Prerelease) {
            $expectedFields += 'make_latest'
            if ($body.make_latest -isnot [string] -or $body.make_latest -cne 'false') { throw 'Prerelease classification changed.' }
        }
        if (@(Compare-Object $expectedFields @($body.psobject.Properties.Name)).Count -ne 0) { throw 'Publication must not change tags or other metadata.' }
        if ($state.Assets.Count -ne 4 -or $state.Metadata.Count -ne 4 -or $state.Uploaded.Count -ne 4 -or $state.TagReads -ne 2) { throw 'Published before all four uploaded digests and the tag were checked.' }
        if ($state.Scenario -eq 'publication-failure') { $global:LASTEXITCODE = 1; return }
        $state.Published++
        $state.Draft = $false
        if ($state.Scenario -eq 'publication-response-lost') { $global:LASTEXITCODE = 1; return }
        $release = Get-FixtureRelease
        if ($state.Scenario -eq 'publication-still-draft') { $release.draft = $true }
        if ($state.Scenario -eq 'publication-wrong-id') { $release.id = 999 }
        $release | ConvertTo-Json -Compress
        return
    }
    throw "Unexpected API request: $method $path"
}

try {
    $defaultVersion = '3.0.0-beta.8+build.001'
    foreach ($version in @($defaultVersion, '3.0.0')) {
        $directory = [IO.Directory]::CreateDirectory((Join-Path $scratch.FullName $version)).FullName
        foreach ($rid in @('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')) {
            $extension = if ($rid -eq 'linux-x64') { 'AppImage' } else { 'zip' }
            [IO.File]::WriteAllText((Join-Path $directory "SIP-Testing-Tool_${rid}_v$version.$extension"), "Different fixture bytes for $rid")
        }
    }
    $cases = @(
        @{ Scenario = 'valid' }, @{ Scenario = 'single-page-draft' }, @{ Scenario = 'new-draft'; New = $true }, @{ Scenario = 'new-empty-list'; New = $true },
        @{ Scenario = 'retry' }, @{ Scenario = 'partial-upload' }, @{ Scenario = 'sidecars' }, @{ Scenario = 'uppercase-digest' },
        @{ Scenario = 'published'; Error = '*already published*'; NoWrites = $true },
        @{ Scenario = 'wrong-tag'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'wrong-release-id'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'invalid-direct-id'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'invalid-direct-draft'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'direct-published'; Error = '*already published*'; NoWrites = $true },
        @{ Scenario = 'published-before-cleanup'; Error = '*already published*'; NoWrites = $true },
        @{ Scenario = 'published-during-verification'; Error = '*already published*' },
        @{ Scenario = 'published-after-final-tag-check'; Error = '*already published*' },
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
        @{ Scenario = 'not-found-after-create'; New = $true },
        @{ Scenario = 'list-failure-after-create'; New = $true },
        @{ Scenario = 'new-large-id'; New = $true },
        @{ Scenario = 'stable-new'; New = $true; Version = '3.0.0' },
        @{ Scenario = 'stable-existing'; Version = '3.0.0' },
        @{ Scenario = 'annotated-tag'; New = $true },
        @{ Scenario = 'nested-tag'; New = $true },
        @{ Scenario = 'create-failure'; New = $true; Error = '*GitHub API failed*' },
        @{ Scenario = 'create-failed-after-creation'; New = $true; Error = '*GitHub API failed*' },
        @{ Scenario = 'creation-conflict'; New = $true; Error = '*GitHub API failed*' },
        @{ Scenario = 'created-published'; New = $true; Error = '*already published*' },
        @{ Scenario = 'created-wrong-prerelease'; New = $true; Error = '*classification*' },
        @{ Scenario = 'created-invalid-prerelease'; New = $true; Error = '*classification*' },
        @{ Scenario = 'created-no-prerelease'; New = $true; Error = '*classification*' },
        @{ Scenario = 'created-wrong-repository'; New = $true; Error = '*intended repository*' },
        @{ Scenario = 'created-wrong-url-id'; New = $true; Error = '*intended repository*' },
        @{ Scenario = 'created-no-url'; New = $true; Error = '*intended repository*' },
        @{ Scenario = 'created-invalid-json'; New = $true; Error = '*JSON*' },
        @{ Scenario = 'created-empty-response'; New = $true; Error = '*Missing GitHub API response*' },
        @{ Scenario = 'created-null-response'; New = $true; Error = '*Missing GitHub API response*' },
        @{ Scenario = 'missing-tag'; New = $true; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'missing-tag'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'wrong-tag-ref'; New = $true; Error = '*requested release tag*'; NoWrites = $true },
        @{ Scenario = 'invalid-tag-sha'; New = $true; Error = '*Invalid GitHub release tag target*'; NoWrites = $true },
        @{ Scenario = 'invalid-tag-type'; New = $true; Error = '*Invalid GitHub release tag target*'; NoWrites = $true },
        @{ Scenario = 'wrong-tag-commit'; New = $true; Error = '*expected commit*'; NoWrites = $true },
        @{ Scenario = 'wrong-annotation'; New = $true; Error = '*annotated GitHub tag*'; NoWrites = $true },
        @{ Scenario = 'cyclic-tag'; New = $true; Error = '*Cyclic GitHub release tag*'; NoWrites = $true },
        @{ Scenario = 'changed-tag-commit'; Error = '*expected commit*' },
        @{ Scenario = 'wrong-direct-prerelease'; Error = '*classification*'; NoWrites = $true },
        @{ Scenario = 'wrong-direct-repository'; Error = '*intended repository*'; NoWrites = $true },
        @{ Scenario = 'direct-array-response'; Error = '*intended release ID and tag*'; NoWrites = $true },
        @{ Scenario = 'new-direct-api-failure'; New = $true; Error = '*GitHub API failed*' },
        @{ Scenario = 'invalid-stale-id'; Error = '*Invalid draft asset ID*'; NoWrites = $true },
        @{ Scenario = 'duplicate-stale-id'; Error = '*Duplicate draft asset ID*'; NoWrites = $true },
        @{ Scenario = 'replaced-upload-id'; Error = '*uploaded asset IDs*' },
        @{ Scenario = 'asset-array-response'; Error = '*metadata does not match*' },
        @{ Scenario = 'changed-final-id'; Error = '*asset set changed*' },
        @{ Scenario = 'changed-final-name'; Error = '*asset set changed*' },
        @{ Scenario = 'changed-final-digest'; Error = '*asset set changed*' },
        @{ Scenario = 'publication-response-lost'; Error = '*GitHub API failed*'; Published = $true },
        @{ Scenario = 'publication-still-draft'; Error = '*classification*'; Published = $true },
        @{ Scenario = 'publication-wrong-id'; Error = '*intended release ID and tag*'; Published = $true },
        @{ Scenario = 'direct-api-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'cleanup-check-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'asset-list-api-failure'; Error = '*GitHub API failed*'; NoWrites = $true },
        @{ Scenario = 'asset-list-after-upload-failure'; Error = '*GitHub API failed*' },
        @{ Scenario = 'nested-asset-object'; Error = '*Invalid GitHub asset metadata*'; NoWrites = $true },
        @{ Scenario = 'upload-check-failure'; Error = '*GitHub API failed*' },
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
        @{ Scenario = 'invalid-asset-state'; Error = '*metadata does not match*' },
        @{ Scenario = 'invalid-asset-name'; Error = '*metadata does not match*' },
        @{ Scenario = 'unuploaded-asset'; Error = '*Unexpected release asset*' },
        @{ Scenario = 'api-failure'; Error = '*GitHub API failed*' },
        @{ Scenario = 'upload-failure'; Error = '*GitHub API failed*' },
        @{ Scenario = 'changed-asset-set'; Error = '*asset set changed*' },
        @{ Scenario = 'changed-final-state-type'; Error = '*asset set changed*' },
        @{ Scenario = 'publication-failure'; Error = '*GitHub API failed*' }
    )
    foreach ($scenario in @('created-no-id', 'created-null-id', 'created-string-id', 'created-zero-id', 'created-negative-id',
        'created-fractional-id', 'created-boolean-id', 'created-wrong-tag', 'created-case-tag', 'created-invalid-draft', 'created-no-draft', 'created-array-response')) {
        $cases += @{ Scenario = $scenario; New = $true; Error = '*intended release ID and tag*' }
    }
    foreach ($scenario in @('upload-no-id', 'upload-string-id', 'upload-zero-id', 'upload-wrong-name', 'upload-starter', 'upload-invalid-state', 'upload-invalid-name', 'upload-duplicate-id', 'upload-array-response')) {
        $cases += @{ Scenario = $scenario; Error = '*Invalid GitHub uploaded asset response*' }
    }
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
        $version = if ($case.Version) { $case.Version } else { $defaultVersion }
        $directory = Join-Path $scratch.FullName $version
        $global:SipReleaseTestState = [pscustomobject]@{
            Scenario = $case.Scenario; Target = [int]$case.Target; BadDigest = $case.BadDigest
            Tag = "v$version"; Commit = 'a' * 40; Prerelease = $version -ne '3.0.0'; New = [bool]$case.New
            Exists = -not $case.New; ReleaseId = 42L
            Draft = $case.Scenario -ne 'published'
            Assets = [Collections.Generic.List[object]]::new(); Calls = [Collections.Generic.List[object]]::new()
            Writes = [Collections.Generic.List[string]]::new(); Uploaded = [Collections.Generic.List[string]]::new()
            Metadata = [Collections.Generic.List[long]]::new(); Deleted = [Collections.Generic.List[long]]::new()
            Published = 0; Created = 0; ListReads = 0; DirectReads = 0; TagReads = 0; InitialAssetCount = 0; CreateCalls = 0; PublishCalls = 0
        }
        $state = $global:SipReleaseTestState
        if ($case.Scenario -in @('retry', 'published', 'published-before-cleanup', 'cleanup-check-failure', 'delete-failure', 'invalid-stale-id', 'duplicate-stale-id')) {
            foreach ($name in @('obsolete-package.zip', 'obsolete-package.zip.sha256', 'unrelated.txt')) {
                $state.Assets.Add(@{ id = 500 + $state.Assets.Count; name = $name; state = 'uploaded' })
            }
        }
        if ($case.Scenario -eq 'invalid-stale-id') { $state.Assets[2].id = '502' }
        if ($case.Scenario -eq 'duplicate-stale-id') { $state.Assets[2].id = 500 }
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
            $output = @(& (Join-Path $PSScriptRoot 'PublishDesktopRelease.ps1') -Directory $directory -Repository local/fixture -Tag "v$version" -Version $version -ExpectedCommit ('a' * 40) 6>$null)
            if ($output.Count -ne 0) { throw 'Publication leaked unexpected pipeline output.' }
        } catch {
            if (-not $case.Error -or $_.Exception.Message -notlike $case.Error) { throw "Unexpected failure for $($case.Scenario): $_`n$($_.ScriptStackTrace)" }
            $rejected = $true
        }
        if ($rejected -ne [bool]$case.Error) { throw "Unexpected publication result for $($case.Scenario)" }
        $state = $global:SipReleaseTestState
        if ($case.Error) {
            if ($state.Published -ne [int][bool]$case.Published) { throw "Published despite $($case.Scenario)" }
            if (-not $case.Published -and $case.Scenario -notin @('published', 'direct-published', 'published-before-cleanup', 'published-during-verification', 'published-after-final-tag-check') -and -not $state.Draft) {
                throw 'A failed publication must leave a draft.'
            }
            if ($case.NoWrites -and $state.Writes.Count -ne 0) { throw "Unsafe mutation for $($case.Scenario)" }
            if (-not $case.Published -and $case.Scenario -ne 'publication-failure' -and $state.PublishCalls -ne 0) {
                throw "Attempted publication despite $($case.Scenario)"
            }
        } elseif ($state.Published -ne 1 -or $state.Assets.Count -ne 4 -or @($state.Metadata | Sort-Object -Unique).Count -ne 4) {
            throw "Successful publication did not check all four asset IDs: $($case.Scenario)"
        }
        if (-not $case.Error) {
            if ($state.Deleted.Count -ne $state.InitialAssetCount) { throw 'Draft retry did not remove every stale asset.' }
            if ($state.Created -ne [int][bool]$case.New -or $state.CreateCalls -ne [int][bool]$case.New) {
                throw 'A retry must reuse its discovered draft.'
            }
            if ($state.New -and $state.DirectReads -lt 1) { throw 'The creation ID must be verified directly.' }
        }
        # Explicit regression guard for both new drafts and existing-draft retries.
        if ($state.ListReads -ne 1) { throw "Publication requires exactly one initial discovery: $($case.Scenario)" }
        if ($state.CreateCalls -gt 1 -or $state.PublishCalls -gt 1) { throw 'An ambiguous mutation must never be retried.' }
        if ($case.New -and $state.CreateCalls -eq 1 -and $case.Error -and $state.Writes.Count -ne 1) { throw 'Failed creation validation must stop after the one POST.' }
        if ($case.Scenario -eq 'upload-failure' -and $state.Assets.Count -ne 3) { throw 'Upload failure did not leave a resumable partial draft.' }
    }
    $version = $defaultVersion
    $directory = Join-Path $scratch.FullName $version
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
            & (Join-Path $PSScriptRoot 'PublishDesktopRelease.ps1') -Directory $local -Repository local/fixture -Tag "v$version" -Version $version -ExpectedCommit ('a' * 40) 6>$null
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
