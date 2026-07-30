#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

function Invoke-ReadOnlyGit {
  param(
    [Parameter(Mandatory)] [string] $Repository,
    [Parameter(Mandatory)] [string[]] $Arguments
  )

  $output = & git -C $Repository @Arguments 2>&1
  return [pscustomobject]@{
    ExitCode = $LASTEXITCODE
    Output = ($output | Out-String).Trim()
  }
}

function Get-RequiredGitOutput {
  param(
    [Parameter(Mandatory)] [string] $Repository,
    [Parameter(Mandatory)] [string[]] $Arguments
  )

  $result = Invoke-ReadOnlyGit -Repository $Repository -Arguments $Arguments
  if ($result.ExitCode -ne 0) {
    throw "Read-only git command failed in ${Repository}: git $($Arguments -join ' ')`n$($result.Output)"
  }
  return $result.Output
}

function Get-RepositoryContext {
  param([Parameter(Mandatory)] [string] $Repository)

  $branch = Get-RequiredGitOutput -Repository $Repository -Arguments @('branch', '--show-current')
  $commit = Get-RequiredGitOutput -Repository $Repository -Arguments @('rev-parse', '--short', 'HEAD')
  $status = Get-RequiredGitOutput -Repository $Repository -Arguments @('status', '--porcelain')
  return [pscustomobject]@{
    Path = $Repository
    Branch = $branch
    Commit = $commit
    Worktree = if ($status) { 'dirty' } else { 'clean' }
  }
}

$config = Get-LocalForkConfig
$version = Get-PinnedOpenCodeVersion -Config $config
$tag = "$($config.OpenCodeReleaseTagPrefix)$version"
$tagRef = "refs/tags/$tag"
$openChamber = Get-RepositoryContext -Repository $config.OpenChamberRoot
$openCode = Get-RepositoryContext -Repository $config.OpenCodeRoot
$tagResult = Invoke-ReadOnlyGit -Repository $config.OpenCodeRoot -Arguments @(
  'rev-parse', '--verify', '--quiet', $tagRef
)

$tagAvailable = $tagResult.ExitCode -eq 0
$containsTag = $false
if ($tagAvailable) {
  $ancestorResult = Invoke-ReadOnlyGit -Repository $config.OpenCodeRoot -Arguments @(
    'merge-base', '--is-ancestor', $tagRef, 'HEAD'
  )
  if ($ancestorResult.ExitCode -notin @(0, 1)) {
    throw "Unable to compare OpenCode HEAD with ${tag}: $($ancestorResult.Output)"
  }
  $containsTag = $ancestorResult.ExitCode -eq 0
}

[pscustomobject]@{
  OpenChamberPath = $openChamber.Path
  OpenChamberBranch = $openChamber.Branch
  OpenChamberCommit = $openChamber.Commit
  OpenChamberWorktree = $openChamber.Worktree
  OpenChamberTarget = "$($config.OfficialRemote)/$($config.OpenChamberOfficialBranch)"
  PinnedOpenCodeSdk = $version
  ExpectedOpenCodeTag = $tag
  OpenCodePath = $openCode.Path
  OpenCodeBranch = $openCode.Branch
  OpenCodeCommit = $openCode.Commit
  OpenCodeWorktree = $openCode.Worktree
  OpenCodeTagAvailable = $tagAvailable
  OpenCodeContainsTag = $containsTag
}
