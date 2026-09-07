#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param([switch] $SkipTypeCheck, [hashtable] $Config)

. (Join-Path $PSScriptRoot 'Common.ps1')

function Set-LocalSdkLink {
  param(
    [Parameter(Mandatory)] [string] $Consumer,
    [Parameter(Mandatory)] [string] $Target
  )

  $scopeDirectory = Join-Path $Consumer 'node_modules\@opencode-ai'
  if (-not (Test-Path -LiteralPath $scopeDirectory -PathType Container)) {
    throw "OpenChamber dependencies are not installed for SDK consumer: $Consumer"
  }
  $dependencyPath = Join-Path $scopeDirectory 'sdk'
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

function New-LocalSdkPackage {
  param(
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $Destination,
    [Parameter(Mandatory)] [Collections.IDictionary] $Catalog
  )

  $sourcePackagePath = Join-Path $Source 'package.json'
  $sourceDist = Join-Path $Source 'dist'
  if (-not (Test-Path -LiteralPath $sourceDist -PathType Container)) {
    throw "Built local SDK dist directory not found: $sourceDist"
  }

  $sourcePackage = Get-Content -LiteralPath $sourcePackagePath -Raw | ConvertFrom-Json -AsHashtable
  $exports = [ordered]@{}
  foreach ($entry in $sourcePackage.exports.GetEnumerator()) {
    if ($entry.Value -isnot [string] -or $entry.Value -notmatch '^\./src/.+\.ts$') {
      throw "Unexpected local SDK export $($entry.Key): $($entry.Value)"
    }
    $file = ($entry.Value -replace '^\./src/', './dist/') -replace '\.ts$', ''
    $exports[$entry.Key] = [ordered]@{
      import = "$file.js"
      types = "$file.d.ts"
    }
  }
  $dependencies = [ordered]@{}
  foreach ($entry in $sourcePackage.dependencies.GetEnumerator()) {
    $version = [string]$entry.Value
    if ($version -eq 'catalog:') {
      if (-not $Catalog.Contains($entry.Key)) {
        throw "Local SDK dependency is missing from the OpenCode workspace catalog: $($entry.Key)"
      }
      $version = [string]$Catalog[$entry.Key]
    } elseif ($version.StartsWith('catalog:', [StringComparison]::Ordinal)) {
      throw "Unsupported named catalog dependency in local SDK package: $($entry.Key) = $version"
    }
    $dependencies[$entry.Key] = $version
  }
  $package = [ordered]@{
    name = $sourcePackage.name
    version = $sourcePackage.version
    type = $sourcePackage.type
    license = $sourcePackage.license
    exports = $exports
    dependencies = $dependencies
  }

  $parent = Split-Path $Destination -Parent
  $temporary = "$Destination.next-$PID"
  New-Item -ItemType Directory -Path $parent -Force | Out-Null
  Remove-LocalSdkPackage -Path $temporary
  try {
    New-Item -ItemType Directory -Path $temporary | Out-Null
    Copy-Item -LiteralPath $sourceDist -Destination $temporary -Recurse -Force
    $package | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $temporary 'package.json') -Encoding utf8NoBOM
    Invoke-NativeCommand -FilePath 'bun' -Arguments @(
      'install', '--cwd', $temporary, '--production', '--ignore-scripts', '--no-save'
    )
    Remove-LocalSdkPackage -Path $Destination
    Move-Item -LiteralPath $temporary -Destination $Destination
  } catch {
    Remove-LocalSdkPackage -Path $temporary
    throw
  }
}

if (-not $Config) { $Config = Get-LocalForkConfig }
$pinnedVersion = Get-PinnedOpenCodeVersion -Config $config
$workspacePackagePath = Join-Path $config.OpenCodeRoot 'package.json'
$workspacePackage = Get-Content -LiteralPath $workspacePackagePath -Raw | ConvertFrom-Json -AsHashtable
$catalog = $workspacePackage.workspaces.catalog
if ($catalog -isnot [Collections.IDictionary]) {
  throw "OpenCode workspace catalog not found: $workspacePackagePath"
}
$sdkPackagePath = Join-Path $config.OpenCodeSdkRoot 'package.json'
if (-not (Test-Path -LiteralPath $sdkPackagePath -PathType Leaf)) {
  throw "Local OpenCode SDK package not found: $sdkPackagePath"
}
$sdkPackage = Get-Content -LiteralPath $sdkPackagePath -Raw | ConvertFrom-Json
if ($sdkPackage.name -ne '@opencode-ai/sdk') {
  throw "Unexpected local SDK package name: $($sdkPackage.name)"
}
if ($sdkPackage.version -ne $pinnedVersion) {
  throw "Local SDK version $($sdkPackage.version) does not match OpenChamber's pinned SDK version $pinnedVersion"
}

Invoke-NativeCommand -FilePath 'bun' -Arguments @(
  'run', '--cwd', $config.OpenCodeClientRoot, 'generate'
)
Remove-Item -LiteralPath (Join-Path $config.OpenCodeSdkRoot 'tsconfig.tsbuildinfo') -Force -ErrorAction SilentlyContinue
Invoke-NativeCommand -FilePath 'bun' -Arguments @(
  'run', '--cwd', $config.OpenCodeSdkRoot, 'build'
)

$requiredOutputs = @(
  'dist\v2\client.js'
  'dist\v2\client.d.ts'
  'dist\v2\index.js'
  'dist\v2\index.d.ts'
  'dist\v2\gen\types.gen.js'
  'dist\v2\gen\types.gen.d.ts'
)
foreach ($relative in $requiredOutputs) {
  $output = Join-Path $config.OpenCodeSdkRoot $relative
  if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "Generated local SDK output not found: $output"
  }
}

try {
  New-LocalSdkPackage -Source $config.OpenCodeSdkRoot -Destination $config.OpenCodeSdkLinkRoot -Catalog $catalog
  foreach ($consumer in $config.OpenChamberSdkConsumers) {
    Set-LocalSdkLink -Consumer $consumer -Target $config.OpenCodeSdkLinkRoot
  }
  Assert-LocalOpenCodeSdkLinked -Config $config
} catch {
  $linkFailure = $_
  Write-Warning 'Local SDK linking failed; restoring OpenChamber dependencies from the frozen lockfile.'
  try {
    Invoke-NativeCommand -FilePath 'bun' -Arguments @(
      'install', '--cwd', $config.OpenChamberRoot, '--frozen-lockfile', '--force'
    )
    Remove-LocalSdkPackage -Path $config.OpenCodeSdkLinkRoot
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

"Local @opencode-ai/sdk $pinnedVersion is linked into all OpenChamber consumers."
