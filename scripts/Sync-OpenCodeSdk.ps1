#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param([switch] $SkipTypeCheck, [hashtable] $Config)

. (Join-Path $PSScriptRoot 'Common.ps1')

function Set-LocalSdkLink {
  param(
    [Parameter(Mandatory)] [string] $Consumer,
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Target
  )

  if (-not (Test-Path -LiteralPath (Join-Path $Consumer 'node_modules') -PathType Container)) {
    throw "OpenChamber dependencies are not installed for SDK consumer: $Consumer"
  }
  $scopeDirectory = Join-Path $Consumer 'node_modules\@opencode'
  New-Item -ItemType Directory -Path $scopeDirectory -Force | Out-Null
  $dependencyPath = Join-Path $scopeDirectory $Name
  if (Test-Path -LiteralPath $dependencyPath) {
    $item = Get-Item -LiteralPath $dependencyPath -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
      Remove-Item -LiteralPath $dependencyPath -Force
    } else {
      Remove-Item -LiteralPath $dependencyPath -Recurse -Force
    }
  }
  $linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
  New-Item -ItemType $linkType -Path $dependencyPath -Target $Target | Out-Null
}

if (-not $Config) { $Config = Get-LocalForkConfig }
$pinnedVersion = Get-PinnedOpenCodeVersion -Config $config
$packages = @('schema', 'protocol', 'client')
foreach ($name in $packages) {
  $manifest = Get-Content -LiteralPath (Join-Path $config.OpenCodeRoot "packages/$name/package.json") -Raw | ConvertFrom-Json
  if ($manifest.name -ne "@opencode/$name" -or $manifest.version -ne $pinnedVersion) {
    throw "Local @opencode/$name must match the OpenChamber pin $pinnedVersion"
  }
}

Invoke-NativeCommand -FilePath 'bun' -Arguments @(
  'run', '--cwd', $config.OpenCodeClientRoot, 'generate'
)
foreach ($name in $packages) {
  Invoke-NativeCommand -FilePath 'bun' -Arguments @('run', '--cwd', (Join-Path $config.OpenCodeRoot "packages/$name"), 'build')
}

$temporary = "$($config.OpenCodeSdkLinkRoot).next-$PID-$([guid]::NewGuid().ToString('N'))"
try {
  Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
    (Join-Path $PSScriptRoot 'Stage-OpenCodeSdk.cs'), '--', $config.OpenCodeRoot, $temporary, $pinnedVersion
  )
  Remove-LocalSdkConsumerLinks -Config $config
  Remove-LocalSdkPackage -Path $config.OpenCodeSdkLinkRoot
  Move-Item -LiteralPath $temporary -Destination $config.OpenCodeSdkLinkRoot
  # Install at the final path: Bun's workspace junctions must not target a renamed staging directory.
  Invoke-NativeCommand -FilePath 'bun' -Arguments @('install', '--cwd', $config.OpenCodeSdkLinkRoot, '--production', '--ignore-scripts')
  Invoke-NativeCommand -FilePath 'bun' -Arguments @('install', '--cwd', $config.OpenCodeSdkLinkRoot, '--production', '--ignore-scripts', '--frozen-lockfile')
  foreach ($consumer in $config.OpenChamberSdkConsumers) {
    foreach ($name in $packages) {
      Set-LocalSdkLink -Consumer $consumer -Name $name -Target (Join-Path $config.OpenCodeSdkLinkRoot "packages/$name")
    }
  }
  Assert-LocalOpenCodeSdkLinked -Config $config
} catch {
  $linkFailure = $_
  Write-Warning 'Local SDK linking failed; restoring OpenChamber dependencies from the frozen lockfile.'
  try {
    Remove-LocalSdkConsumerLinks -Config $config
    Invoke-NativeCommand -FilePath 'bun' -Arguments @(
      'install', '--cwd', $config.OpenChamberRoot, '--frozen-lockfile', '--force'
    )
    Remove-LocalSdkPackage -Path $config.OpenCodeSdkLinkRoot
    Remove-LocalSdkPackage -Path $temporary
  } catch {
    Write-Warning "Automatic SDK link cleanup also failed: $($_.Exception.Message)"
  }
  throw $linkFailure
}

if (-not $SkipTypeCheck) {
  foreach ($script in @('type-check:ui', 'type-check:web', 'vscode:type-check')) {
    Invoke-NativeCommand -FilePath 'bun' -Arguments @(
      'run', '--cwd', $config.OpenChamberRoot, $script
    )
  }
}

"Local @opencode client/schema/protocol $pinnedVersion are linked into all OpenChamber consumers."
