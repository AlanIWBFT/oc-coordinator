#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$config = Get-LocalForkConfig
Invoke-NativeCommand -FilePath 'bun' -Arguments @(
  'install', '--cwd', $config.OpenChamberRoot, '--frozen-lockfile', '--force'
)

if (Test-LocalOpenCodeSdkLinked -Config $config) {
  throw 'The local SDK is still linked after restoring OpenChamber dependencies.'
}
Remove-LocalSdkPackage -Path $config.OpenCodeSdkLinkRoot

"Official @opencode-ai/sdk restored from OpenChamber's frozen lockfile."
