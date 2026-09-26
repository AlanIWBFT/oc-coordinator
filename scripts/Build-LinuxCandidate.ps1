#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param(
  [string] $SourceRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
  [string] $BuildRoot = (Join-Path $HOME '.local/state/openchamber-fork/build'),
  [switch] $PrepareOnly
)

. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $IsLinux -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
  throw 'Linux Candidate builds currently require a native Linux x86-64 host.'
}
$bunVersion = (& bun --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $bunVersion -notmatch '^1\.4\.') { throw "Linux builds require Bun 1.4.x, got: $bunVersion" }
foreach ($tool in @('node', 'rsync')) { Get-Command $tool -ErrorAction Stop | Out-Null }
if (-not $PrepareOnly) { Get-Command dotnet -ErrorAction Stop | Out-Null }
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
if ($BuildRoot.StartsWith("$SourceRoot/", [StringComparison]::Ordinal) -or $BuildRoot -eq $SourceRoot) {
  throw 'Use a separate Linux-native build directory, not the Windows source tree.'
}
$marker = Join-Path $BuildRoot '.coordinator-build'
if (Test-Path -LiteralPath $BuildRoot) {
  if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { throw "Refusing to synchronize an unmanaged build directory: $BuildRoot" }
  if ((Get-Content -LiteralPath $marker -Raw).Trim() -ne $SourceRoot) { throw 'Build directory belongs to another source root.' }
}
foreach ($name in @('opencode', 'openchamber')) {
  if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot "$name/package.json") -PathType Leaf)) { throw "Missing source package: $name" }
}
New-Item -ItemType Directory -Path $BuildRoot -Force | Out-Null
Set-Content -LiteralPath $marker -Value $SourceRoot -Encoding utf8NoBOM
foreach ($name in @('opencode', 'openchamber')) {
  Invoke-NativeCommand rsync @(
    '-a', '--delete', '--omit-dir-times', '--no-perms',
    '--exclude=.git', '--exclude=node_modules', '--exclude=dist', '--exclude=dist-bundle',
    '--exclude=.cache', '--exclude=.turbo', '--exclude=*.tsbuildinfo', '--exclude=.env', '--exclude=.env.local',
    '--exclude=/packages/electron/resources/web-dist', '--exclude=/packages/electron/resources/opencode-cli',
    "$SourceRoot/$name/", "$BuildRoot/$name/"
  )
}
$config = @{
  OpenChamberRoot = "$BuildRoot/openchamber"
  OpenCodeRoot = "$BuildRoot/opencode"
  OpenCodeClientRoot = "$BuildRoot/opencode/packages/client"
  OpenCodeSdkLinkRoot = "$BuildRoot/generated/opencode-sdk"
  OpenChamberSdkConsumers = @("$BuildRoot/openchamber", "$BuildRoot/openchamber/packages/ui", "$BuildRoot/openchamber/packages/web", "$BuildRoot/openchamber/packages/vscode")
}
Invoke-WithProcessEnvironment -Variables @{
  HUSKY = '0'
  OPENCODE_CHANNEL = 'dev'
  OPENCODE_VERSION = Get-PinnedOpenCodeVersion -Config $config
  OPENCODE_RELEASE = $null
  OPENCHAMBER_OPENCODE_SOURCE_DIR = $config.OpenCodeRoot
  OPENCHAMBER_TARGET_ARCH = 'x64'
} -ScriptBlock {
  foreach ($root in @($config.OpenCodeRoot, $config.OpenChamberRoot)) {
    Invoke-NativeCommand bun @('install', '--cwd', $root, '--frozen-lockfile')
  }
  if ($PrepareOnly) { return }
  & (Join-Path $PSScriptRoot 'Sync-OpenCodeSdk.ps1') -Config $config
  Invoke-NativeCommand bun @('run', '--cwd', (Join-Path $config.OpenChamberRoot 'packages/sdk'), 'build')
  $electron = Join-Path $config.OpenChamberRoot 'packages/electron'
  foreach ($step in @('build:web-assets', 'prepare:opencode-cli', 'verify:opencode-cli', 'bundle:main', 'rebuild:native')) {
    Invoke-NativeCommand bun @('run', '--cwd', $electron, $step)
  }
  Invoke-InDirectory -Path $electron -ScriptBlock {
    Invoke-NativeCommand node @('scripts/package.mjs', '--linux', 'AppImage', '--x64', '--publish=never')
  }
  Invoke-NativeCommand bun @('run', '--cwd', $electron, 'verify:linux-appimage')
  "Linux Candidate artifacts: $electron/dist"
}
