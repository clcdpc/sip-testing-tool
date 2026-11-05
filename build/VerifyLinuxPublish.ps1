param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference = 'Stop'

# Stable runtime components distinguish self-contained output without pinning a patch
# version or enumerating every framework assembly and native dependency.
foreach ($name in @('sip-testing-tool', 'sip-testing-tool.dll', 'sip-testing-tool.deps.json',
    'sip-testing-tool.runtimeconfig.json', 'System.Private.CoreLib.dll',
    'libcoreclr.so', 'libhostfxr.so', 'libhostpolicy.so')) {
    $file = Get-Item -LiteralPath (Join-Path $Directory $name) -ErrorAction SilentlyContinue
    if ($null -eq $file -or $file.PSIsContainer -or $file.Length -eq 0) {
        throw "Missing self-contained Linux publish file: $name"
    }
}
$stream = [IO.File]::OpenRead((Join-Path $Directory 'sip-testing-tool'))
try {
    $header = [byte[]]::new(20)
    $stream.ReadExactly($header)
    if ($header[0] -ne 0x7f -or [Text.Encoding]::ASCII.GetString($header, 1, 3) -cne 'ELF' -or
        $header[4] -ne 2 -or $header[5] -ne 1 -or [BitConverter]::ToUInt16($header, 18) -ne 62) {
        throw 'The Linux apphost must be a native Linux x64 executable.'
    }
} finally { $stream.Dispose() }
$config = Get-Content -LiteralPath (Join-Path $Directory 'sip-testing-tool.runtimeconfig.json') -Raw | ConvertFrom-Json
$framework = @($config.runtimeOptions.includedFrameworks | Where-Object { $_.name -ceq 'Microsoft.NETCore.App' })
if ($config.runtimeOptions.tfm -cne 'net10.0' -or $config.runtimeOptions.framework -or $config.runtimeOptions.frameworks -or
    $framework.Count -ne 1 -or $framework[0].version -cnotmatch '^10\.0\.\d+$') {
    throw 'The Linux application must include its .NET 10 runtime in a self-contained runtime configuration.'
}
