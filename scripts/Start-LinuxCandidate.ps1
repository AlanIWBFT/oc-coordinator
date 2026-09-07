#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param(
  [string] $BuildRoot = (Join-Path $HOME '.local/state/openchamber-fork/build'),
  [string] $SandboxRoot = (Join-Path $HOME '.local/state/openchamber-fork/cow'),
  [string[]] $CowPath = @(),
  [ValidateRange(0, 3600)] [int] $SmokeSeconds = 0
)

. (Join-Path $PSScriptRoot 'Linux-Cow.ps1')
if (-not $IsLinux) { throw 'This launcher requires Linux.' }
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
if (-not (Test-Path -LiteralPath (Join-Path $BuildRoot '.coordinator-build') -PathType Leaf)) {
  throw 'Use the managed Linux build directory created by Build-LinuxCandidate.ps1.'
}
$electron = Join-Path $BuildRoot 'openchamber/packages/electron'
$package = Get-Content -LiteralPath (Join-Path $electron 'package.json') -Raw | ConvertFrom-Json
$binary = Join-Path $electron "dist/OpenChamber-$($package.version)-linux-x86_64.AppImage"
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw "Candidate AppImage not found: $binary" }

# Use the desktop login environment, not the managed OpenCode process's injected endpoints and credentials.
$raw = & systemctl --user show-environment --output=json
if ($LASTEXITCODE -ne 0) { throw 'Unable to obtain the systemd user login environment; refusing to inherit the current agent environment.' }
$loginEnvironment = $raw | ConvertFrom-Json -AsHashtable
foreach ($name in @('HOME', 'PATH', 'SHELL')) {
  if (-not $loginEnvironment[$name]) { throw "Login environment is missing $name" }
}
$shell = $loginEnvironment['SHELL']
if ([IO.Path]::GetFileName($shell) -notin @('bash', 'zsh', 'sh')) { throw 'Candidate login currently supports bash, zsh and sh.' }
$node = (Get-Command node -ErrorAction Stop).Source
$arguments = @('-l', '-c', 'exec "$@"', 'openchamber-candidate', $node, (Join-Path $PSScriptRoot 'linux-candidate-entry.mjs'), $binary, [string]$SmokeSeconds)
$result = Invoke-LinuxCow -Executable $shell -ArgumentList $arguments -SandboxRoot $SandboxRoot -Environment $loginEnvironment -CowPath (@($BuildRoot, (Get-Location).Path) + $CowPath) -Desktop
Write-Host "Candidate exited with code $($result.ExitCode). CoW files: $($result.Directory)"
exit $result.ExitCode
