#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param([ValidateSet('x64', 'arm64')] [string] $Architecture = 'x64')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
  throw "Visual Studio Setup locator not found: $vswhere"
}

# Keep the local build on the supported VS 2022 toolset without relying on its changing instance ID.
$instanceJson = & $vswhere @(
  '-latest',
  '-products', '*',
  '-version', '[17.0,18.0)',
  '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64',
  '-format', 'json'
)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($instanceJson | Out-String))) {
  throw 'No complete Visual Studio 2022 instance with the VC++ x64/x86 tools was found.'
}
$instance = @($instanceJson | ConvertFrom-Json)[0]
if (-not $instance.instanceId -or -not $instance.installationPath) {
  throw 'Visual Studio Setup locator returned an incomplete instance record.'
}

$module = Join-Path $instance.installationPath 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll'
if (-not (Test-Path -LiteralPath $module -PathType Leaf)) {
  throw "Visual Studio Developer Shell module not found: $module"
}

Import-Module $module -Force
$InformationPreference = 'SilentlyContinue'
$devShellArchitecture = if ($Architecture -eq 'x64') { 'amd64' } else { 'arm64' }
Enter-VsDevShell -VsInstallPath $instance.installationPath -Arch $devShellArchitecture -HostArch amd64 -SkipAutomaticLocation | Out-Null
$compiler = Get-Command cl.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $compiler) {
  throw "Visual Studio Developer Shell did not make cl.exe available for $Architecture."
}

$environment = [ordered]@{}
Get-ChildItem Env: | ForEach-Object {
  $environment[$_.Name] = $_.Value
}
Write-Output "__OPENCHAMBER_VS_ENV__$($environment | ConvertTo-Json -Compress)"
