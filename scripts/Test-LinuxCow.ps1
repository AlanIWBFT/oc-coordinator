#requires -Version 7.0
#requires -PSEdition Core

[CmdletBinding()]
param([switch] $Desktop)

. (Join-Path $PSScriptRoot 'Linux-Cow.ps1')
$policyEnvironment = @{ HOME = $HOME; OPENCHAMBER_DATA_DIR = '/opt/openchamber-cow-not-created'; OPENCODE_DB = '/opt/openchamber-cow-not-created/test.db' }
$policyRoots = @(Get-LinuxCowRoots -Environment $policyEnvironment)
if ('/opt' -notin $policyRoots) { throw 'External data/DB directory was not covered by its existing ancestor.' }
if (Test-Path -LiteralPath '/opt/openchamber-cow-not-created') { throw 'Path discovery created the configured data directory on the host.' }
try {
  Get-LinuxCowRoots -Environment @{ HOME = $HOME } -CowPath @('/') | Out-Null
  throw 'Policy accepted root-wide CoW.'
} catch {
  if ($_.Exception.Message -eq 'Policy accepted root-wide CoW.') { throw }
}
$id = [Guid]::NewGuid().ToString('N')
$fixtures = @("/tmp/opencode/cow-test-$id", (Join-Path $HOME ".local/state/openchamber-fork/cow-test-$id"), (Join-Path (Split-Path $PSScriptRoot -Parent) ".tmp/cow-test-$id"))
$runtimeSocket = Join-Path $env:XDG_RUNTIME_DIR "cow-test-$id.sock"
$runtimeFile = Join-Path $env:XDG_RUNTIME_DIR "cow-test-$id.data"
function Get-UpperPath($Result, [string] $Target) {
  $mount = $Result.Mounts | Where-Object { $Target -eq $_.Source -or $Target.StartsWith("$($_.Source)/", [StringComparison]::Ordinal) } | Sort-Object { $_.Source.Length } -Descending | Select-Object -First 1
  if (-not $mount) { throw "No CoW mapping for $Target" }
  return Join-Path $mount.Upper $Target.Substring($mount.Source.Length).TrimStart('/')
}
$server = $null
try {
  foreach ($directory in $fixtures) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    [IO.File]::WriteAllText("$directory/change", 'original')
    [IO.File]::WriteAllText("$directory/delete", 'original')
    [IO.File]::WriteAllText("$directory/rename", 'original')
  }
  $start = [Diagnostics.ProcessStartInfo]::new('/usr/bin/node')
  $start.UseShellExecute = $false
  $start.RedirectStandardOutput = $true
  foreach ($arg in @('-e', 'let ready = 0; for (const socket of process.argv.slice(1)) require("node:net").createServer(s => s.end("host")).listen(socket, () => { if (++ready === 2) console.log("ready"); });', "$($fixtures[0])/host.sock", $runtimeSocket)) { $start.ArgumentList.Add($arg) }
  $server = [Diagnostics.Process]::Start($start)
  $ready = $server.StandardOutput.ReadLineAsync()
  if (-not $ready.Wait(5000) -or $ready.Result -ne 'ready') { throw 'Socket fixture did not start.' }
  $code = @'
const fs = require('node:fs');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const [runtimeSocket, runtimeFile, desktop, ...directories] = process.argv.slice(1);
assert.throws(() => fs.writeFileSync('/etc/openchamber-cow-permission-test', 'forbidden'), error => ['EACCES', 'EROFS'].includes(error.code));
const mounts = fs.readFileSync('/proc/self/mountinfo', 'utf8').split('\n');
assert.ok(!mounts.some(line => line.split(' ')[4] === '/' && line.includes(' - fuse.fuse-overlayfs ')));
assert.ok(!mounts.some(line => line.split(' ')[4] === '/usr' && line.includes(' - fuse.fuse-overlayfs ')));
assert.equal(fs.statSync('/tmp').mode & 0o7777, 0o1777);
for (const root of ['/tmp', require('node:os').homedir()]) {
  const file = require('node:path').join(root, `${require('node:path').basename(directories[0])}-root`);
  fs.writeFileSync(file, 'root-copy-up');
  assert.equal(fs.readFileSync(file, 'utf8'), 'root-copy-up');
}
if (desktop === 'True') fs.writeFileSync(runtimeFile, 'shared-runtime');
for (const dir of directories) {
  assert.equal(fs.readFileSync(`${dir}/change`, 'utf8'), 'original');
  fs.writeFileSync(`${dir}/change`, 'sandbox');
  fs.unlinkSync(`${dir}/delete`);
  fs.renameSync(`${dir}/rename`, `${dir}/renamed`);
  fs.symlinkSync(`${dir}/change`, `${dir}/link`);
  fs.writeFileSync(`${dir}/link`, 'via-symlink');
  execFileSync(process.execPath, ['-e', 'require("node:fs").writeFileSync(process.argv[1], "child")', `${dir}/child`]);
  assert.equal(fs.readFileSync(`${dir}/change`, 'utf8'), 'via-symlink');
  assert.equal(fs.readFileSync(`${dir}/renamed`, 'utf8'), 'original');
  assert.equal(fs.existsSync(`${dir}/delete`), false);
  assert.equal(fs.readFileSync(`${dir}/child`, 'utf8'), 'child');
}
const detached = require('node:child_process').spawn(process.execPath, ['-e',
  'const fs = require("node:fs"); fs.writeFileSync(process.argv[1], "ready"); setTimeout(() => fs.writeFileSync(process.argv[2], "escaped"), 1500);',
  `${directories[0]}/detached-ready`, `${directories[0]}/detached-late`], { detached: true, stdio: 'ignore' });
detached.unref();
const deadline = Date.now() + 3000;
while (!fs.existsSync(`${directories[0]}/detached-ready`) && Date.now() < deadline) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 10);
assert.ok(fs.existsSync(`${directories[0]}/detached-ready`));
const sharedSocket = require('node:net').connect(runtimeSocket);
let sharedData = '';
sharedSocket.on('data', data => { sharedData += data; });
sharedSocket.on('end', () => { assert.equal(sharedData, 'host'); console.log('Host runtime socket is shared without a proxy.'); });
sharedSocket.on('error', error => { console.error(error.message); process.exit(1); });
sharedSocket.setTimeout(3000, () => { console.error('Runtime socket check timed out'); process.exit(1); });
const socket = require('node:net').connect(`${directories[0]}/host.sock`);
socket.on('connect', () => { console.error('Host Unix socket was reachable through CoW'); process.exit(1); });
socket.on('error', error => {
  assert.ok(['ECONNREFUSED', 'EACCES', 'ENOENT'].includes(error.code), error.message);
  console.log('Inside CoW: file operations, child processes, permissions and host socket isolation passed.');
});
socket.setTimeout(3000, () => { console.error('Socket check timed out'); process.exit(1); });
'@
  $result = Invoke-LinuxCow -Executable /usr/bin/node -ArgumentList (@('-e', $code, $runtimeSocket, $runtimeFile, [string][bool]$Desktop) + $fixtures) -Desktop:$Desktop
  if ($result.ExitCode -ne 0) { throw "CoW fixture failed with code $($result.ExitCode)" }
  foreach ($mount in $result.Mounts) {
    & mountpoint -q $mount.Merged
    if ($LASTEXITCODE -eq 0) { throw "CoW filesystem remained mounted: $($mount.Source)" }
  }
  if ($Desktop) {
    $layout = Get-Content -LiteralPath (Join-Path $result.Directory 'layout.json') -Raw | ConvertFrom-Json
    $state = & systemctl --user show $layout.Scope --property=LoadState --value
    if ($LASTEXITCODE -ne 0 -or $state -ne 'not-found') { throw 'Candidate scope remained after exit.' }
  }
  if ($Desktop -and [IO.File]::ReadAllText($runtimeFile) -ne 'shared-runtime') { throw 'Desktop runtime file was not shared.' }
  foreach ($root in @('/tmp', $HOME)) {
    if (Test-Path -LiteralPath (Join-Path $root "cow-test-$id-root")) { throw 'Creating a file directly in a CoW root modified the host.' }
  }
  Start-Sleep -Seconds 2
  if (Test-Path -LiteralPath (Get-UpperPath $result "$($fixtures[0])/detached-late")) { throw 'A detached descendant survived sandbox exit.' }
  foreach ($directory in $fixtures) {
    foreach ($name in @('change', 'delete', 'rename')) {
      if ([IO.File]::ReadAllText("$directory/$name") -ne 'original') { throw "Host file changed: $directory/$name" }
    }
    foreach ($name in @('renamed', 'link', 'child', 'detached-ready', 'detached-late')) {
      if (Test-Path -LiteralPath "$directory/$name") { throw "Sandbox file escaped to host: $directory/$name" }
    }
    if ([IO.File]::ReadAllText((Get-UpperPath $result "$directory/change")) -ne 'via-symlink') { throw "Missing retained CoW data: $directory" }
  }
  $result = Invoke-LinuxCow -Executable /usr/bin/node -ArgumentList @('-e', 'const fs = require("node:fs"); if (fs.readFileSync(process.argv[1], "utf8") !== "original") process.exit(1); process.exit(23);', "$($fixtures[0])/change")
  if ($result.ExitCode -ne 23) { throw 'Fresh-layer or exit-code propagation check failed.' }
  'Host unchanged across tmpfs, home and the source filesystem.'
} finally {
  if ($null -ne $server) {
    if (-not $server.HasExited) { $server.Kill(); $server.WaitForExit() }
    $server.Dispose()
  }
  foreach ($file in @($runtimeSocket, $runtimeFile)) { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
  foreach ($directory in $fixtures) {
    if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory -Recurse -Force }
  }
}
