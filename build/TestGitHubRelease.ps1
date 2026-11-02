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
        if (-not $path.StartsWith('repos/local/fixture/releases/', [StringComparison]::Ordinal)) { throw "Unexpected API path: $path" }
        if ('Accept: application/vnd.github+json' -cnotin $arguments -or 'X-GitHub-Api-Version: 2022-11-28' -cnotin $arguments) {
            throw 'Release metadata must use the GitHub REST JSON API.'
        }
        if ('DELETE' -cin $arguments) {
            if (-not $state.Draft) { throw 'Attempted to delete a published asset.' }
            $assetId = [long]($path -split '/')[-1]
            $asset = @($state.Assets | Where-Object id -eq $assetId)
            if ($asset.Count -ne 1) { throw "Deleted an asset outside this draft: $assetId" }
            $null = $state.Assets.Remove($asset[0])
            $state.Deleted.Add($assetId)
            return
        }
        if ($path -match '/releases/(tags/[^/]+|42)$') {
            $tag = if ($state.Scenario -eq 'wrong-tag') { 'v9.9.9' } else { $state.Tag }
            $releaseId = if ($state.Scenario -eq 'wrong-release-id' -and $path.EndsWith('/42')) { 43 } else { 42 }
            @{ id = $releaseId; tag_name = $tag; draft = $state.Draft } | ConvertTo-Json -Compress
            return
        }
        if ($path -eq 'repos/local/fixture/releases/42/assets?per_page=100') {
            if ('--paginate' -cnotin $arguments -or '--slurp' -cnotin $arguments) { throw 'The entire release asset list must be read.' }
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
        if ($path -match '/releases/assets/(\d+)$') {
            $assetId = [long]$Matches[1]
            $asset = @($state.Assets | Where-Object id -eq $assetId)
            if ($asset.Count -ne 1) { throw "Read an asset outside this draft: $assetId" }
            $state.Metadata.Add($assetId)
            if ($state.Scenario -eq 'api-failure') { $global:LASTEXITCODE = 1; return }
            $metadata = $asset[0].Clone()
            if ($state.Scenario -eq 'wrong-asset-name') { $metadata.name = 'another-package.zip' }
            if ($state.Scenario -eq 'wrong-asset-id') { $metadata.id = 999 }
            $metadata | ConvertTo-Json -Compress
            return
        }
        throw "Unexpected API request: $path"
    }
    if ($arguments[0] -ne 'release' -or $arguments[2] -cne $state.Tag -or 'local/fixture' -cnotin $arguments) {
        throw "Unexpected gh invocation: $arguments"
    }
    switch ($arguments[1]) {
        'view' {
            if (-not $state.Exists) { $global:LASTEXITCODE = 1; return }
            @{ isDraft = $state.Draft } | ConvertTo-Json -Compress
        }
        'create' {
            if ($state.Exists -or '--verify-tag' -cnotin $arguments -or '--draft' -cnotin $arguments) { throw 'Unsafe draft creation.' }
            $state.Exists = $true
        }
        'edit' {
            if (-not $state.Draft) { throw 'Attempted to edit a published release.' }
            if ('--prerelease=true' -cnotin $arguments -or '--latest=false' -cnotin $arguments) { throw 'Prerelease classification changed.' }
            if ('--draft=false' -cin $arguments) {
                if ($state.Assets.Count -ne 4 -or $state.Metadata.Count -ne 4) { throw 'Published before all four digests were checked.' }
                $state.Published++
                $state.Draft = $false
            }
        }
        'upload' {
            if (-not $state.Draft -or $state.Assets.Count -ne 0 -or '--clobber' -cin $arguments) { throw 'Uploads require a clean draft.' }
            $paths = @($arguments | Select-Object -Skip 3 -First 4)
            foreach ($path in $paths) {
                $id = 100 + $state.Assets.Count
                $original = [IO.File]::ReadAllBytes($path)
                if ($state.Scenario -eq 'changed-local-bytes') { [IO.File]::AppendAllText($path, 'changed during upload') }
                $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
                [IO.File]::WriteAllBytes($path, $original)
                if ($state.Scenario -ne 'uppercase-digest') { $hash = $hash.ToLowerInvariant() }
                $state.Assets.Add(@{ id = $id; name = [IO.Path]::GetFileName($path); state = 'uploaded'; digest = "sha256:$hash" })
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
                'upload-failure' { $global:LASTEXITCODE = 1 }
            }
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
        @{ Scenario = 'valid' }, @{ Scenario = 'new-draft' }, @{ Scenario = 'retry' }, @{ Scenario = 'uppercase-digest' },
        @{ Scenario = 'published'; Error = '*already published*' },
        @{ Scenario = 'wrong-tag'; Error = '*intended release ID and tag*' },
        @{ Scenario = 'wrong-release-id'; Error = '*intended release ID and tag*' },
        @{ Scenario = 'missing-asset'; Error = '*exactly the four expected*' },
        @{ Scenario = 'extra-asset'; Error = '*exactly the four expected*' },
        @{ Scenario = 'duplicate-name'; Error = '*exactly the four expected*' },
        @{ Scenario = 'duplicate-id'; Error = '*exactly the four expected*' },
        @{ Scenario = 'unexpected-name'; Error = '*Unexpected release asset*' },
        @{ Scenario = 'changed-local-bytes'; Error = '*SHA-256 mismatch*' },
        @{ Scenario = 'wrong-asset-name'; Error = '*metadata does not match*' },
        @{ Scenario = 'wrong-asset-id'; Error = '*metadata does not match*' },
        @{ Scenario = 'unuploaded-asset'; Error = '*Unexpected release asset*' },
        @{ Scenario = 'api-failure'; Error = '*GitHub API failed*' },
        @{ Scenario = 'upload-failure'; Error = '*Could not upload all release assets*' },
        @{ Scenario = 'changed-asset-set'; Error = '*asset set changed*' }
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
            Tag = "v$version"; Exists = $case.Scenario -ne 'new-draft'; Draft = $case.Scenario -ne 'published'
            Assets = [Collections.Generic.List[object]]::new(); Calls = [Collections.Generic.List[object]]::new()
            Metadata = [Collections.Generic.List[long]]::new(); Deleted = [Collections.Generic.List[long]]::new(); Published = 0
        }
        if ($case.Scenario -eq 'retry') {
            foreach ($name in @('obsolete-package.zip', 'obsolete-package.zip.sha256', 'unrelated.txt')) {
                $global:SipReleaseTestState.Assets.Add(@{ id = 500 + $global:SipReleaseTestState.Assets.Count; name = $name; state = 'uploaded' })
            }
        }
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
            if ($case.Scenario -ne 'published' -and -not $state.Draft) { throw 'A failed publication must leave a draft.' }
            if ($case.Scenario -eq 'published' -and @($state.Calls | Where-Object { $_[1] -ne 'view' }).Count -ne 0) {
                throw 'Published releases must not be mutated.'
            }
        } elseif ($state.Published -ne 1 -or $state.Assets.Count -ne 4 -or @($state.Metadata | Sort-Object -Unique).Count -ne 4) {
            throw "Successful publication did not check all four asset IDs: $($case.Scenario)"
        }
        if ($case.Scenario -eq 'retry' -and (($state.Deleted | Sort-Object) -join ',') -cne '500,501,502') { throw 'Draft retry did not remove every stale asset.' }
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
