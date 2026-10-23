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
