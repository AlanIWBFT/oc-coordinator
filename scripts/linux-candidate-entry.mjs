import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
const mounts = fs.readFileSync('/proc/self/mountinfo', 'utf8').split('\n')
  .filter(line => line.includes(' - fuse.fuse-overlayfs '))
  .map(line => line.split(' ')[4].replace(/\\([0-7]{3})/g, (_, octal) => String.fromCharCode(parseInt(octal, 8))));
const requireCow = value => {
  let existing = path.resolve(value);
  while (!fs.existsSync(existing)) existing = path.dirname(existing);
  const resolved = fs.realpathSync(existing);
  if (!mounts.some(root => resolved === root || resolved.startsWith(`${root}/`))) {
    throw new Error(`Path is outside CoW coverage: ${value}. Add its directory with -CowPath.`);
  }
};
requireCow(os.homedir());
requireCow(os.tmpdir());
for (const name of ['XDG_CONFIG_HOME', 'XDG_DATA_HOME', 'XDG_CACHE_HOME', 'XDG_STATE_HOME', 'OPENCHAMBER_DATA_DIR',
  'OPENCHAMBER_CHATS_DIR', 'OPENCHAMBER_MANAGED_PROCESS_REGISTRY', 'OPENCODE_CONFIG_DIR', 'OPENCODE_CONFIG', 'OPENCODE_DB']) {
  const value = process.env[name];
  if (value && value !== ':memory:') requireCow(path.isAbsolute(value) ? value : path.join(os.homedir(), value));
}

// Chromium's host-instance locks are transient IPC state, not reusable profile data.
const profile = path.join(process.env.XDG_CONFIG_HOME || path.join(os.homedir(), '.config'), 'OpenChamber');
requireCow(profile);
for (const name of ['SingletonLock', 'SingletonSocket', 'SingletonCookie']) {
  fs.rmSync(path.join(profile, name), { force: true });
}

const [binary, seconds] = process.argv.slice(2);
if (!binary?.endsWith('.AppImage')) throw new Error('Expected a Candidate AppImage.');
requireCow(path.dirname(binary));
const extraction = fs.mkdtempSync(path.join(os.tmpdir(), 'openchamber-candidate-'));
const result = spawnSync(binary, ['--appimage-extract'], { cwd: extraction, stdio: ['ignore', 'ignore', 'inherit'], timeout: 120000 });
if (result.status !== 0) throw new Error('Candidate AppImage extraction failed.');
const appDir = path.join(extraction, 'squashfs-root');
const child = spawn(path.join(appDir, 'AppRun'), [], {
  stdio: 'inherit',
  env: { ...process.env, APPIMAGE: binary, APPDIR: appDir },
});
let timer;
let forceTimer;
if (Number(seconds) > 0) {
  timer = setTimeout(() => {
    child.kill('SIGTERM');
    forceTimer = setTimeout(() => child.kill('SIGKILL'), 10000);
  }, Number(seconds) * 1000);
}
child.on('error', error => {
  console.error(error.message);
  clearTimeout(timer);
  clearTimeout(forceTimer);
  process.exitCode = 1;
});
child.on('exit', (code, signal) => {
  clearTimeout(timer);
  clearTimeout(forceTimer);
  fs.rmSync(extraction, { recursive: true, force: true });
  process.exitCode = code ?? (signal === 'SIGTERM' && Number(seconds) > 0 ? 0 : 1);
});
