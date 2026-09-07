#requires -Version 7.0
#requires -PSEdition Core

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TrayItems {
  $raw = & busctl --user --json=short get-property org.kde.StatusNotifierWatcher /StatusNotifierWatcher org.kde.StatusNotifierWatcher RegisteredStatusNotifierItems
  if ($LASTEXITCODE -ne 0) { throw 'Unable to query the tray host.' }
  return @(($raw | ConvertFrom-Json).data)
}

$before = @(Get-TrayItems)
$start = [Diagnostics.ProcessStartInfo]::new('pwsh')
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($arg in @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Start-LinuxCandidate.ps1'), '-SmokeSeconds', '60')) { $start.ArgumentList.Add($arg) }
$candidate = [Diagnostics.Process]::Start($start)
$stdout = $candidate.StandardOutput.ReadToEndAsync()
$stderr = $candidate.StandardError.ReadToEndAsync()
try {
  $item = $null
  $deadline = [DateTime]::UtcNow.AddSeconds(40)
  while (-not $item -and -not $candidate.HasExited -and [DateTime]::UtcNow -lt $deadline) {
    foreach ($entry in @(Get-TrayItems)) {
      if ($entry -in $before) { continue }
      $name = $entry.Split('/', 2)[0]
      $raw = & busctl --user --json=short call org.freedesktop.DBus /org/freedesktop/DBus org.freedesktop.DBus GetConnectionUnixProcessID s $name
      if ($LASTEXITCODE -ne 0) { continue }
      $peer = ($raw | ConvertFrom-Json).data[0]
      $ancestor = $peer
      # Only interact with an actual Electron descendant of this test's launcher.
      for ($depth = 0; $depth -lt 20 -and $ancestor -gt 1; $depth++) {
        if ($ancestor -eq $candidate.Id) { $item = $entry; break }
        $status = [IO.File]::ReadAllText("/proc/$ancestor/status")
        if ($status -notmatch '(?m)^PPid:\s+(\d+)$') { break }
        $ancestor = [int]$Matches[1]
      }
      if ($item) { break }
    }
    if (-not $item) { Start-Sleep -Milliseconds 250 }
  }
  if (-not $item) { throw 'Candidate did not register a tray item on the shared session bus.' }
  $name, $object = $item.Split('/', 2)
  $object = "/$object"
  Write-Host "Candidate registered its tray item: $item"
  $run = @(Get-ChildItem -LiteralPath (Join-Path $HOME '.local/state/openchamber-fork/cow') -Directory -Filter "*-$($candidate.Id)-*")
  if ($run.Count -ne 1) { throw 'Could not identify the test CoW layout.' }
  $cowLayout = Get-Content -LiteralPath (Join-Path $run[0].FullName 'layout.json') -Raw | ConvertFrom-Json
  if ($cowLayout.LauncherPid -ne $candidate.Id) { throw 'CoW layout belongs to another launcher.' }
  Write-Host 'Candidate owns its native D-Bus connection; no tray proxy is involved.'
  Start-Sleep -Seconds 8
  $raw = & busctl --user --json=short get-property $name $object org.kde.StatusNotifierItem Menu
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read Candidate tray menu path.' }
  $menu = ($raw | ConvertFrom-Json).data
  $raw = & busctl --user --json=short -- call $name $menu com.canonical.dbusmenu GetLayout iias 0 -1 1 label
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read Candidate tray menu.' }
  $layout = ($raw | ConvertFrom-Json -AsHashtable).data[1]
  $controls = @{}
  foreach ($entry in $layout[2]) {
    $label = $entry.data[1]['label']
    if ($label -and $label.data -in @('Show Window', 'Hide Window', 'Close')) { $controls[$label.data] = $entry.data[0] }
  }
  foreach ($label in @('Hide Window', 'Show Window', 'Close')) {
    if (-not $controls.ContainsKey($label)) { throw "Candidate tray is missing $label" }
    & busctl --user call $name $menu com.canonical.dbusmenu Event isvu $controls[$label] clicked i 0 0
    if ($LASTEXITCODE -ne 0) { throw "Candidate tray action failed: $label" }
    Write-Host "Candidate tray action dispatched: $label"
    Start-Sleep -Milliseconds 500
  }
  if (-not $candidate.WaitForExit(15000)) { throw 'Candidate did not exit through its tray Close action.' }
  if ($candidate.ExitCode -ne 0) { throw "Candidate exited with code $($candidate.ExitCode)." }
  if ($item -in @(Get-TrayItems)) { throw 'Tray registration remained after Candidate exited.' }
  foreach ($mount in $cowLayout.Mounts) {
    & mountpoint -q $mount.Merged
    if ($LASTEXITCODE -eq 0) { throw "CoW remained mounted after tray exit: $($mount.Source)" }
  }
  if (Test-Path -LiteralPath "/proc/$peer") { throw 'Candidate remained running after tray exit.' }
  $state = & systemctl --user show $cowLayout.Scope --property=LoadState --value
  if ($LASTEXITCODE -ne 0 -or $state -ne 'not-found') { throw 'Candidate scope remained after tray exit.' }
  'Candidate tray registration, menu actions and normal exit passed.'
} finally {
  if (-not $candidate.WaitForExit(75000)) { $candidate.Kill($true); $candidate.WaitForExit() }
  $output = $stdout.GetAwaiter().GetResult()
  $errors = $stderr.GetAwaiter().GetResult()
  Write-Host (($output -split '\r?\n' | Where-Object { $_ -match '^CoW files|^Candidate exited' }) -join "`n")
  Write-Host (($errors -split '\r?\n' | Where-Object { $_ -match 'dbus|StatusNotifier|tray|Exception|throw|Error:|EACCES|EROFS|CoW' }) -join "`n")
  $candidate.Dispose()
}
