#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$config = Get-LocalForkConfig
Remove-LocalSdkConsumerLinks -Config $config
Invoke-NativeCommand -FilePath 'bun' -Arguments @(
  'install', '--cwd', $config.OpenChamberRoot, '--frozen-lockfile', '--force'
)

if (Test-LocalOpenCodeSdkLinked -Config $config) {
  throw 'The local SDK is still linked after restoring OpenChamber dependencies.'
}
Remove-LocalSdkPackage -Path $config.OpenCodeSdkLinkRoot

"Official @opencode client/schema/protocol restored from OpenChamber's frozen lockfile."
