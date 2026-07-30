#requires -Version 7.0
#requires -PSEdition Core

. (Join-Path $PSScriptRoot 'Common.ps1')

$config = Get-LocalForkConfig
$architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
$unpackedName = if ($architecture -eq [Runtime.InteropServices.Architecture]::Arm64) {
  'win-arm64-unpacked'
} elseif ($architecture -eq [Runtime.InteropServices.Architecture]::X64) {
  'win-unpacked'
} else {
  throw "Unsupported Windows architecture: $architecture"
}
$binary = Join-Path $config.OpenChamberRoot "packages\electron\dist\$unpackedName\OpenChamber.exe"
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) {
  throw "Candidate app not found: $binary`nRun: dotnet .\scripts\ForkCoordinator.cs -- build-candidate"
}
$sandboxie = $config.SandboxieStart
if (-not (Test-Path -LiteralPath $sandboxie -PathType Leaf)) {
  throw "Sandboxie Start.exe not found: $sandboxie"
}

"Starting sandboxed Candidate: $binary"
"Sandboxie box: $($config.CandidateSandbox)"
Start-Process -FilePath $sandboxie -ArgumentList @('/wait', "/box:$($config.CandidateSandbox)", $binary) -Wait
