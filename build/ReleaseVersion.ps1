function Get-DesktopReleaseVersion {
    param(
        [Parameter(Mandatory)][string]$Version,
        [switch]$Tag
    )
    $value = $Version
    if ($Tag) {
        if (-not $value.StartsWith('v', [StringComparison]::Ordinal)) {
            throw 'Release tags must begin with v, for example v2.1.2-beta.1.'
        }
        $value = $value.Substring(1)
    }
    $match = [regex]::Match($value, '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z')
    if (-not $match.Success) {
        throw 'Release versions must use MAJOR.MINOR.PATCH with an optional prerelease suffix, for example 2.1.2-beta.1.'
    }
    if ($match.Groups['prerelease'].Success) {
        foreach ($identifier in $match.Groups['prerelease'].Value.Split('.')) {
            if ($identifier -cmatch '\A[0-9]+\z' -and $identifier.Length -gt 1 -and $identifier[0] -eq '0') {
                throw 'Numeric prerelease identifiers must not have leading zeroes.'
            }
        }
    }
    [pscustomobject]@{ Version = $value; IsPrerelease = $match.Groups['prerelease'].Success }
}

function Assert-DesktopApplicationVersion {
    param(
        [Parameter(Mandatory)][string]$ApplicationPath,
        [Parameter(Mandatory)][string]$Version
    )
    $null = Get-DesktopReleaseVersion -Version $Version
    $path = (Resolve-Path -LiteralPath $ApplicationPath).Path
    # Read the managed attribute without loading application dependencies or running code.
    Add-Type -AssemblyName System.Reflection.Metadata
    $actual = $null
    $stream = [IO.File]::OpenRead($path)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            if ($pe.HasMetadata) {
                $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                foreach ($handle in $reader.GetAssemblyDefinition().GetCustomAttributes()) {
                    $attribute = $reader.GetCustomAttribute($handle)
                    if ($attribute.Constructor.Kind -ne [System.Reflection.Metadata.HandleKind]::MemberReference) { continue }
                    $constructor = $reader.GetMemberReference([System.Reflection.Metadata.MemberReferenceHandle]$attribute.Constructor)
                    if ($constructor.Parent.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) { continue }
                    $type = $reader.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$constructor.Parent)
                    if ($reader.GetString($type.Namespace) -ceq 'System.Reflection' -and
                        $reader.GetString($type.Name) -ceq 'AssemblyInformationalVersionAttribute') {
                        $value = $reader.GetBlobReader($attribute.Value)
                        if ($value.ReadUInt16() -ne 1) { throw 'Invalid informational version attribute.' }
                        $actual = $value.ReadSerializedString()
                        break
                    }
                }
            } elseif ($IsWindows) {
                # The Windows single-file apphost copies the informational version to ProductVersion.
                $actual = [Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion
            }
        } finally { $pe.Dispose() }
    } finally { $stream.Dispose() }
    $separator = if ($Version.Contains('+')) { '.' } else { '+' }
    # The SDK may append SourceRevisionId, preserving any metadata already present in the tag.
    $withRevision = '\A' + [regex]::Escape($Version + $separator) + '[0-9a-f]{40}(?:[0-9a-f]{24})?\z'
    if ($actual -cne $Version -and $actual -cnotmatch $withRevision) {
        throw "Application informational version '$actual' does not match expected '$Version': $path. Rebuild with -p:Version=$Version before packaging."
    }
    Write-Host "Verified application version $actual"
}
