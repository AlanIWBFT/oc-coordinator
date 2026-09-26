#requires -Version 7.0
#requires -PSEdition Core

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function Get-LocalForkConfig {
  if (-not $IsWindows) {
    throw 'The default config contains Windows paths. Use Build-LinuxCandidate.ps1 on Linux.'
  }
  $configPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'local-fork.config.psd1'
  if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Local fork config not found: $configPath"
  }
  return Import-PowerShellDataFile -LiteralPath $configPath
}

function Invoke-WithProcessEnvironment {
  param(
    [Parameter(Mandatory)] [hashtable] $Variables,
    [Parameter(Mandatory)] [scriptblock] $ScriptBlock
  )

  $previous = @{}
  foreach ($entry in $Variables.GetEnumerator()) {
    $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
  }

  try {
    & $ScriptBlock
  } finally {
    foreach ($entry in $previous.GetEnumerator()) {
      [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
  }
}

function Invoke-NativeCommand {
  param(
    [Parameter(Mandatory)] [string] $FilePath,
    [string[]] $Arguments = @()
  )

  & $FilePath @Arguments
  $exitCode = $LASTEXITCODE
  if ($exitCode -ne 0) {
    throw "Command failed with exit code ${exitCode}: $FilePath $($Arguments -join ' ')"
  }
}

function Get-BunRuntimeTranspilerCachePath {
  $profile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
  if (-not $profile) {
    throw 'Unable to resolve the user profile for the Bun runtime transpiler cache.'
  }
  return Join-Path $profile '.bun\install\cache\@t@'
}

function Invoke-BunBuildCommand {
  param([string[]] $Arguments = @())

  Invoke-WithProcessEnvironment -Variables @{
    XDG_CACHE_HOME = $null
    BUN_RUNTIME_TRANSPILER_CACHE_PATH = Get-BunRuntimeTranspilerCachePath
  } -ScriptBlock {
    Invoke-NativeCommand -FilePath 'bun' -Arguments $Arguments
  }
}

function Remove-LocalSdkPackage {
  param([Parameter(Mandatory)] [string] $Path)

  if (-not (Test-Path -LiteralPath $Path)) {
    return
  }
  $nodeModules = Join-Path $Path 'node_modules'
  if (Test-Path -LiteralPath $nodeModules) {
    $item = Get-Item -LiteralPath $nodeModules -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
      Remove-Item -LiteralPath $nodeModules -Force
    } else {
      Remove-Item -LiteralPath $nodeModules -Recurse -Force
    }
  }
  Remove-Item -LiteralPath $Path -Recurse -Force
}

function Get-PinnedOpenCodeVersion {
  param([Parameter(Mandatory)] [hashtable] $Config)

  $packagePath = Join-Path $Config.OpenChamberRoot 'package.json'
  $package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
  $version = $package.dependencies.'@opencode/client'
  if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "OpenChamber does not pin a valid @opencode/client version: $version"
  }
  foreach ($consumer in $Config.OpenChamberSdkConsumers) {
    $manifest = Get-Content -LiteralPath (Join-Path $consumer 'package.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($name in @('@opencode/client', '@opencode/schema')) {
      if ($manifest.dependencies.ContainsKey($name) -and $manifest.dependencies[$name] -ne $version) {
        throw "SDK pin mismatch in ${consumer}: $name must be $version"
      }
    }
  }
  $electron = Get-Content -LiteralPath (Join-Path $Config.OpenChamberRoot 'packages/electron/package.json') -Raw | ConvertFrom-Json
  if ($electron.opencodeCli.version -ne $version) { throw 'Bundled OpenCode CLI and client versions must match.' }
  return $version
}

function Remove-LocalSdkConsumerLinks {
  param([Parameter(Mandatory)] [hashtable] $Config)

  $root = [IO.Path]::GetFullPath($Config.OpenCodeSdkLinkRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
  $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
  foreach ($consumer in $Config.OpenChamberSdkConsumers) {
    foreach ($name in @('@opencode/client', '@opencode/schema', '@opencode/protocol', '@opencode-ai/sdk')) {
      $path = Join-Path $consumer "node_modules/$name"
      $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
      if (-not $item -or -not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or -not $item.LinkTarget) { continue }
      $target = [IO.Path]::GetFullPath($item.LinkTarget, $item.Parent.FullName)
      if ($target.Equals($root, $comparison) -or $target.StartsWith("$root$([IO.Path]::DirectorySeparatorChar)", $comparison)) {
        Remove-Item -LiteralPath $path -Force
      }
    }
  }
}

function Get-ResolvedOpenCodeSdkPath {
  param(
    [Parameter(Mandatory)] [string] $Consumer,
    [Parameter(Mandatory)] [string] $Import
  )

  $output = & node --input-type=module -e 'console.log(import.meta.resolve(process.argv[1]))' $Import 2>&1
  $exitCode = $LASTEXITCODE
  if ($exitCode -ne 0) {
    throw "Unable to resolve $Import from ${Consumer}: $($output | Out-String)"
  }
  $uri = [Uri](($output | Out-String).Trim())
  return [IO.Path]::GetFullPath($uri.LocalPath)
}

function Invoke-InDirectory {
  param(
    [Parameter(Mandatory)] [string] $Path,
    [Parameter(Mandatory)] [scriptblock] $ScriptBlock
  )

  Push-Location -LiteralPath $Path
  try {
    & $ScriptBlock
  } finally {
    Pop-Location
  }
}

function Test-LocalOpenCodeSdkLinked {
  param([Parameter(Mandatory)] [hashtable] $Config)

  $separator = [IO.Path]::DirectorySeparatorChar
  $expected = [IO.Path]::GetFullPath($Config.OpenCodeSdkLinkRoot).TrimEnd($separator)
  $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
  $packages = @(
    @{ Name = 'client'; Import = '@opencode/client' },
    @{ Name = 'schema'; Import = '@opencode/schema' },
    @{ Name = 'protocol'; Import = '@opencode/protocol/api' }
  )
  $origins = @($Config.OpenChamberSdkConsumers) + @($packages | ForEach-Object { Join-Path $expected "packages/$($_.Name)" })
  foreach ($consumer in $origins) {
    foreach ($package in $packages) {
      $resolved = Invoke-InDirectory -Path $consumer -ScriptBlock {
        Get-ResolvedOpenCodeSdkPath -Consumer $consumer -Import $package.Import
      }
      $packageRoot = [IO.Path]::GetFullPath((Join-Path $expected "packages/$($package.Name)/dist")).TrimEnd($separator)
      if (-not $resolved.StartsWith("$packageRoot$separator", $comparison) -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        return $false
      }
    }
  }
  return $true
}

function Assert-LocalOpenCodeSdkLinked {
  param([Parameter(Mandatory)] [hashtable] $Config)

  if (-not (Test-LocalOpenCodeSdkLinked -Config $Config)) {
    throw 'The local client/schema/protocol SDK graph is not linked into every OpenChamber consumer. Run Sync-OpenCodeSdk.ps1 first.'
  }
}
