#requires -Version 7.0
#requires -PSEdition Core

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LinuxCowRoots {
  param([Collections.IDictionary] $Environment, [string[]] $CowPath = @())

  $homePath = [string]$Environment['HOME']
  if (-not $homePath -or -not [IO.Path]::IsPathRooted($homePath)) { throw 'CoW requires an absolute HOME.' }
  $paths = [Collections.Generic.List[string]]::new()
  $paths.AddRange([string[]]@($homePath, '/tmp', '/var/tmp'))
  foreach ($directory in @('/mnt', '/media', '/run/media')) {
    if (Test-Path -LiteralPath $directory -PathType Container) { $paths.Add($directory) }
  }
  # Resolve standard data roots as well: any of these may be a symlink outside HOME.
  foreach ($relative in @('.config/openchamber', '.config/OpenChamber', '.config/opencode', '.config/autostart', '.local/share/opencode',
    '.cache', '.cache/opencode', '.local/state', '.local/state/opencode', '.agents', '.ssh', '.gnupg', '.bun', '.npm')) { $paths.Add((Join-Path $homePath $relative)) }
  foreach ($name in @('XDG_CONFIG_HOME', 'XDG_DATA_HOME', 'XDG_CACHE_HOME', 'XDG_STATE_HOME', 'TMPDIR',
    'OPENCHAMBER_DATA_DIR', 'OPENCHAMBER_CHATS_DIR', 'OPENCHAMBER_MANAGED_PROCESS_REGISTRY', 'OPENCODE_CONFIG_DIR')) {
    if ($Environment[$name]) { $paths.Add([string]$Environment[$name]) }
  }
  foreach ($name in @('OPENCODE_CONFIG', 'OPENCODE_DB')) {
    $value = [string]$Environment[$name]
    if ($value -and $value -ne ':memory:' -and [IO.Path]::IsPathRooted($value)) { $paths.Add((Split-Path $value -Parent)) }
  }
  $paths.AddRange($CowPath)
  $resolved = foreach ($value in $paths) {
    if (-not [IO.Path]::IsPathRooted($value)) { throw "Use an absolute CoW/data directory: $value" }
    $directory = & realpath -m -- $value
    if ($LASTEXITCODE -ne 0) { throw "Cannot resolve CoW directory: $value" }
    # Cover creation of a configured directory without creating it on the host.
    while (-not (Test-Path -LiteralPath $directory -PathType Container)) { $directory = Split-Path $directory -Parent }
    if ($directory -in @('/', '/run', '/dev', '/proc', '/sys', '/usr', '/etc') -or $directory -match '^/(dev|proc|sys)/|[:,\\\r\n]') {
      throw "CoW path resolves to a system/runtime root: $value -> $directory"
    }
    $directory
  }
  $roots = [Collections.Generic.List[string]]::new()
  foreach ($directory in @($resolved | Sort-Object -Unique -CaseSensitive | Sort-Object Length)) {
    if (-not @($roots | Where-Object { $directory -eq $_ -or $directory.StartsWith("$_/", [StringComparison]::Ordinal) }).Count) { $roots.Add($directory) }
  }
  return $roots.ToArray()
}

function Invoke-LinuxCow {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)] [string] $Executable,
    [string[]] $ArgumentList = @(),
    [string] $SandboxRoot = (Join-Path $HOME '.local/state/openchamber-fork/cow'),
    [hashtable] $Environment,
    [string[]] $CowPath = @(),
    [switch] $Desktop
  )

  if (-not $IsLinux) { throw 'CoW execution requires Linux.' }
  foreach ($tool in @('bwrap', 'fuse-overlayfs', 'fusermount3', 'mountpoint', 'setpriv', 'realpath', 'stat', 'setfattr')) { Get-Command $tool -ErrorAction Stop | Out-Null }
  $runtimeEnvironment = if ($null -ne $Environment) { $Environment } else { [Environment]::GetEnvironmentVariables() }
  $roots = @(Get-LinuxCowRoots -Environment $runtimeEnvironment -CowPath $CowPath)
  $SandboxRoot = [IO.Path]::GetFullPath($SandboxRoot)
  New-Item -ItemType Directory -Path $SandboxRoot -Force | Out-Null
  $SandboxRoot = & realpath -e -- $SandboxRoot
  if ($LASTEXITCODE -ne 0 -or $SandboxRoot -match '[:,\\\r\n]' -or $SandboxRoot -eq '/') { throw 'Unsupported CoW storage path.' }
  $run = Join-Path $SandboxRoot ("{0}-{1}-{2}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $PID, [Guid]::NewGuid().ToString('N').Substring(0, 8))
  New-Item -ItemType Directory -Path $run | Out-Null
  & chmod 700 $run
  if ($LASTEXITCODE -ne 0) { throw 'Unable to restrict CoW directory permissions.' }
  Write-Host "CoW files (retained after exit): $run"
  $mounts = [Collections.Generic.List[object]]::new()
  $shared = [Collections.Generic.List[string]]::new()
  $desktopLock = $null
  try {
    $arguments = [Collections.Generic.List[string]]::new()
    $arguments.AddRange([string[]]@('--die-with-parent', '--new-session', '--unshare-user', '--cap-drop', 'ALL',
      '--ro-bind', '/', '/', '--proc', '/proc', '--dev', '/dev', '--ro-bind', '/sys', '/sys'))
    if (-not $Desktop) { $arguments.Add('--unshare-pid') }
    foreach ($source in $roots) {
      $layer = Join-Path $run ([string]$mounts.Count)
      foreach ($name in @('upper', 'work', 'merged')) { New-Item -ItemType Directory -Path "$layer/$name" -Force | Out-Null }
      # Otherwise xattr_permissions=2 initializes every mount root as root:root 0555.
      $metadata = & stat -Lc '%u:%g:%a' -- $source
      if ($LASTEXITCODE -ne 0) { throw "Cannot read CoW root metadata: $source" }
      & setfattr -n user.containers.override_stat -v $metadata -- "$layer/upper"
      if ($LASTEXITCODE -ne 0) { throw "Cannot preserve CoW root metadata: $source" }
      $mount = [pscustomobject]@{ Source = $source; Upper = "$layer/upper"; Merged = "$layer/merged"; Process = $null; Mounted = $false }
      $mounts.Add($mount)
      $start = [Diagnostics.ProcessStartInfo]::new('setpriv')
      $start.UseShellExecute = $false
      # User xattrs retain copied ownership without chown privileges on the host.
      foreach ($arg in @('--pdeathsig', 'TERM', '--', 'fuse-overlayfs', '-f', '-o',
        "lowerdir=$source,upperdir=$layer/upper,workdir=$layer/work,xattr_permissions=2", $mount.Merged)) { $start.ArgumentList.Add($arg) }
      $mount.Process = [Diagnostics.Process]::Start($start)
      $deadline = [DateTime]::UtcNow.AddSeconds(10)
      do {
        & mountpoint -q $mount.Merged
        $mount.Mounted = $LASTEXITCODE -eq 0
        if ($mount.Mounted) { break }
        if ($mount.Process.HasExited) { throw "CoW filesystem failed for $source" }
        Start-Sleep -Milliseconds 50
      } while ([DateTime]::UtcNow -lt $deadline)
      if (-not $mount.Mounted) { throw "Timed out mounting CoW directory: $source" }
      $arguments.AddRange([string[]]@('--bind', $mount.Merged, $source))
    }
    if ($Desktop) {
      $runtime = [string]$runtimeEnvironment['XDG_RUNTIME_DIR']
      if (-not $runtime.StartsWith('/run/user/', [StringComparison]::Ordinal) -or -not (Test-Path -LiteralPath $runtime -PathType Container)) {
        throw 'Desktop integration requires XDG_RUNTIME_DIR under /run/user/.'
      }
      if (@($roots | Where-Object { $_ -eq $runtime -or $_.StartsWith("$runtime/", [StringComparison]::Ordinal) -or $runtime.StartsWith("$_/", [StringComparison]::Ordinal) }).Count) {
        throw 'A CoW data directory overlaps the shared desktop runtime. Choose a data directory outside XDG_RUNTIME_DIR.'
      }
      Get-Command systemd-run -ErrorAction Stop | Out-Null
      # Keep one test instance per desktop while preserving native host PID identity.
      $desktopLock = [IO.File]::Open((Join-Path $runtime 'openchamber-candidate.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
      $arguments.AddRange([string[]]@('--bind', $runtime, $runtime, '--dev-bind', '/dev/shm', '/dev/shm'))
      $shared.Add($runtime)
      $shared.Add('/dev/shm')
      if (-not $runtimeEnvironment['WAYLAND_DISPLAY'] -and -not $runtimeEnvironment['DISPLAY']) { throw 'No desktop display is available.' }
      if (Test-Path -LiteralPath '/tmp/.X11-unix' -PathType Container) {
        $arguments.AddRange([string[]]@('--ro-bind', '/tmp/.X11-unix', '/tmp/.X11-unix'))
        $shared.Add('/tmp/.X11-unix')
      }
      $devices = @('/dev/dri', '/dev/snd') + @(Get-ChildItem -LiteralPath /dev -Filter 'nvidia*' | Select-Object -ExpandProperty FullName)
      foreach ($device in $devices) {
        if (Test-Path -LiteralPath $device) { $arguments.AddRange([string[]]@('--dev-bind', $device, $device)); $shared.Add($device) }
      }
      # Authentication and display sockets can live outside XDG_RUNTIME_DIR, including under HOME/tmp.
      $sockets = @([string]$runtimeEnvironment['SSH_AUTH_SOCK'])
      if ([IO.Path]::IsPathRooted([string]$runtimeEnvironment['WAYLAND_DISPLAY'])) { $sockets += [string]$runtimeEnvironment['WAYLAND_DISPLAY'] }
      if ($runtimeEnvironment['DBUS_SESSION_BUS_ADDRESS'] -match '^unix:path=([^,;]+)') { $sockets += [Uri]::UnescapeDataString($Matches[1]) }
      if (Get-Command gpgconf -ErrorAction SilentlyContinue) {
        foreach ($kind in @('agent-socket', 'agent-ssh-socket')) { $sockets += & gpgconf --list-dirs $kind 2>$null }
      }
      foreach ($socket in @($sockets | Where-Object { $_ -and [IO.Path]::IsPathRooted($_) } | Sort-Object -Unique)) {
        if (Test-Path -LiteralPath $socket) { $arguments.AddRange([string[]]@('--ro-bind', $socket, $socket)); $shared.Add($socket) }
      }
    }
    $arguments.AddRange([string[]]@('--tmpfs', $SandboxRoot, '--chdir', (Get-Location).Path))
    $scope = if ($Desktop) { "openchamber-candidate-$PID-$([Guid]::NewGuid().ToString('N')).scope" } else { $null }
    $layout = [pscustomobject]@{
      Version = 2; LauncherPid = $PID; Desktop = [bool]$Desktop
      Mounts = @($mounts | Select-Object Source, Upper, Merged)
      Shared = $shared.ToArray()
      Scope = $scope
    }
    $layout | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$run/layout.json" -Encoding utf8NoBOM
    Write-Host "CoW roots: $($roots -join ', ')"
    if ($Desktop) { Write-Host "Shared desktop interfaces: $($shared -join ', '); host network, IPC and D-Bus (no proxy)." }
    $arguments.Add('--')
    $arguments.Add($Executable)
    $arguments.AddRange($ArgumentList)
    $childStart = [Diagnostics.ProcessStartInfo]::new($(if ($Desktop) { 'systemd-run' } else { 'bwrap' }))
    $childStart.UseShellExecute = $false
    if ($Desktop) {
      foreach ($arg in @('--user', '--scope', '--collect', '--quiet', "--unit=$scope", '--property=TimeoutStopSec=5s', '--expand-environment=no', '--', 'bwrap')) {
        $childStart.ArgumentList.Add($arg)
      }
    }
    foreach ($arg in $arguments) { $childStart.ArgumentList.Add($arg) }
    if ($null -ne $Environment) {
      $childStart.Environment.Clear()
      foreach ($entry in $Environment.GetEnumerator()) { $childStart.Environment[$entry.Key] = [string]$entry.Value }
    }
    $child = [Diagnostics.Process]::Start($childStart)
    try {
      while (-not $child.WaitForExit(250)) {
        foreach ($mount in $mounts) { if ($mount.Process.HasExited) { throw "CoW filesystem exited: $($mount.Source)" } }
      }
      return [pscustomobject]@{ ExitCode = $child.ExitCode; Directory = $run; Mounts = $layout.Mounts }
    } finally {
      if (-not $child.HasExited) { $child.Kill($true); $child.WaitForExit() }
      $child.Dispose()
      if ($Desktop) {
        $loadState = & systemctl --user show $scope --property=LoadState --value
        if ($LASTEXITCODE -ne 0) { throw "Cannot inspect Candidate scope cleanup: $scope" }
        if ($loadState -ne 'not-found') {
          & systemctl --user stop $scope
          if ($LASTEXITCODE -ne 0) { throw "Cannot stop Candidate scope: $scope" }
        }
      }
    }
  } finally {
    $cleanupFailures = [Collections.Generic.List[string]]::new()
    for ($index = $mounts.Count - 1; $index -ge 0; $index--) {
      $mount = $mounts[$index]
      if ($mount.Mounted) {
        & fusermount3 -u $mount.Merged
        if ($LASTEXITCODE -ne 0) {
          & fusermount3 -uz $mount.Merged
          if ($LASTEXITCODE -ne 0) { $cleanupFailures.Add($mount.Merged) }
        }
      }
      if ($null -ne $mount.Process) {
        if (-not $mount.Process.WaitForExit(3000)) { $mount.Process.Kill(); $mount.Process.WaitForExit() }
        $mount.Process.Dispose()
      }
    }
    if ($null -ne $desktopLock) { $desktopLock.Dispose() }
    if ($cleanupFailures.Count) { throw "Could not detach CoW mounts; do not delete them: $($cleanupFailures -join ', ')" }
  }
}
