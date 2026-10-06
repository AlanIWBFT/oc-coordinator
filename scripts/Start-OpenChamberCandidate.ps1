#requires -Version 7.0
#requires -PSEdition Core

param(
  [switch] $ClearSandbox
)

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
  throw "Candidate app not found: $binary`nRun: dotnet .\scripts\ForkCoordinator.cs -- candidate-build"
}
$sandboxie = $config.SandboxieStart
if (-not (Test-Path -LiteralPath $sandboxie -PathType Leaf)) {
  throw "Sandboxie Start.exe not found: $sandboxie"
}

if ($ClearSandbox) {
  "Clearing Sandboxie box: $($config.CandidateSandbox)"
  foreach ($command in @('/terminate', 'delete_sandbox')) {
    $cleanup = Start-Process -FilePath $sandboxie -ArgumentList @("/box:$($config.CandidateSandbox)", $command) -UseNewEnvironment -Wait -PassThru
    if ($cleanup.ExitCode -ne 0) {
      throw "Sandboxie $command failed with exit code $($cleanup.ExitCode)."
    }
  }
}

"Starting sandboxed Candidate: $binary"
"Sandboxie box: $($config.CandidateSandbox)"
# This script can run as a child of Stable -> managed OpenCode -> shell tool. Sandboxie
# isolates files and registry state, but its Start.exe still inherits that process environment.
# Without a new environment, Candidate can reuse Stable values such as OPENCODE_BINARY,
# OPENCODE_SERVER_PASSWORD, OPENCODE_PID, OPENCODE_MANAGED_SHUTDOWN, OPENCODE, AGENT,
# OPENCHAMBER_RUNTIME, OPENCHAMBER_DIST_DIR, OPENCHAMBER_OPENCODE_CWD, and the Stable
# bundled CLI directory added to PATH. UseNewEnvironment rebuilds the environment from the
# Windows User and Machine scopes before Sandboxie starts Candidate.
$p = Start-Process -FilePath $sandboxie -ArgumentList @('/wait', "/box:$($config.CandidateSandbox)", $binary, "--no-sandbox") -UseNewEnvironment -PassThru
"Candidate started, waiting for it to exit..."
$p | Wait-Process
"Candidate exited."
