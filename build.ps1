param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$solutionPath = Join-Path $PSScriptRoot 'sip-testing-tool.sln'
$feedPath = Join-Path $PSScriptRoot 'local\offline-feed'
$restoreArguments = @('restore', $solutionPath)
if (Test-Path -LiteralPath (Join-Path $feedPath 'Newtonsoft.Json.13.0.4.nupkg')) {
    $restoreArguments += @('--source', $feedPath)
}

& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }

& dotnet build $solutionPath --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    $checksPath = Join-Path $PSScriptRoot 'tests\sip-tests\sip-tests.csproj'
    & dotnet run --project $checksPath --configuration $Configuration --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
}
