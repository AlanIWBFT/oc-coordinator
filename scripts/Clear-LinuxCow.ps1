#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding(SupportsShouldProcess)]
param([string] $SandboxRoot = (Join-Path $HOME '.local/state/openchamber-fork/cow'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'CoW cleanup requires Linux.' }
if (-not (Test-Path -LiteralPath $SandboxRoot)) {
  Write-Host 'No CoW data to clean.'
  return
}
$root = Get-Item -LiteralPath $SandboxRoot -Force
if (-not $root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
  throw 'SandboxRoot must be a directory, not a symbolic link.'
}
$rootPath = (& realpath -e -- $root.FullName | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or -not $rootPath -or $rootPath -eq '/') { throw 'Cannot resolve a safe CoW directory.' }
$lock = $null
try {
  if ($env:XDG_RUNTIME_DIR) {
    $lockPath = Join-Path $env:XDG_RUNTIME_DIR 'openchamber-candidate.lock'
    if (Test-Path -LiteralPath $lockPath) {
      $lock = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    }
  }
  $runs = @(Get-ChildItem -LiteralPath $rootPath -Force)
  foreach ($run in $runs) {
    if (-not $run.PSIsContainer -or ($run.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $run.Name -notmatch '^\d{8}-\d{6}-\d+-[a-f0-9]{8}$') {
      throw "Unexpected entry; refusing to clean this directory: $($run.FullName)"
    }
  }
  $raw = & findmnt --json --list --output TARGET
  if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect mounts; cleanup cancelled.' }
  $active = @(($raw | ConvertFrom-Json).filesystems | Where-Object {
    $_.target -eq $rootPath -or $_.target.StartsWith("$rootPath/", [StringComparison]::Ordinal)
  })
  if ($active.Count) { throw "CoW is still mounted; close Candidate first: $($active.target -join ', ')" }
  foreach ($run in $runs) {
    if ($PSCmdlet.ShouldProcess($run.FullName, 'Delete retained CoW data')) {
      Remove-Item -LiteralPath $run.FullName -Recurse -Force
      Write-Host "Removed: $($run.FullName)"
    }
  }
  if (-not $runs.Count) { Write-Host 'No CoW runs to clean.' }
} finally {
  if ($null -ne $lock) { $lock.Dispose() }
}
