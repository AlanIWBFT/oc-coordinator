# Local OpenChamber and OpenCode Fork Workflow

This directory coordinates coupled local-fork API development without modifying either repository's machine-specific configuration.

## Prerequisites

Use PowerShell Core 7 or newer through `pwsh`, not Windows PowerShell 5.1 through `powershell.exe`. Windows packaging requires a complete Visual Studio 2022 instance with the VC++ x64/x86 tools and Windows SDK. The coordinator discovers it through `vswhere` and initializes its Developer Shell by the discovered installation path before native Electron modules rebuild.

Candidate isolation requires Sandboxie-Plus. The default launcher is `C:\Program Files\Sandboxie-Plus\Start.exe` and the default box is `OpenChamberCandidate`; both are configurable in `local-fork.config.psd1`.

## Layout

- `E:\OpenChamber\openchamber`: local OpenChamber fork.
- `E:\OpenChamber\opencode`: local OpenCode fork and source build input.
- `E:\OpenChamber\bun-v1.4.2-release\bun.exe`: verified official Bun 1.4.2 runtime used by Windows Candidate and release builds.
- `E:\OpenChamber\openchamber\packages\electron\dist\win-unpacked`: x64 Candidate app built from the local checkout.
- `E:\OpenChamber\openchamber\packages\electron\dist\win-arm64-unpacked`: ARM64 Candidate app built from the local checkout.
- `E:\OpenChamber\release`: atomically prepared Windows release directories.
- `E:\OpenChamber\coordinator\generated\opencode-sdk`: generated publish-shaped local SDK package used by junction consumers.

The installed release package is Stable. It is an ordinary user installation, not a coordinator-managed slot. The coordinator has no Stable, Rescue, external CLI copy, runtime-root tree, manifest, or promotion command.

## Candidate Isolation

### Linux x86-64 Candidate

Linux builds use Bun 1.4.x, Node.js 22 or newer, PowerShell Core 7, and rsync. The existing Windows Bun selection and repository `bun@1.3.14` declarations remain unchanged.

```powershell
pwsh -NoProfile -File scripts/Build-LinuxCandidate.ps1
```

The script synchronizes the current OpenCode and OpenChamber sources, including uncommitted edits, into `~/.local/state/openchamber-fork/build`. It excludes Windows dependencies and build output, installs Linux dependencies from the frozen lockfiles, regenerates both client contracts, and links the publish-shaped local SDK into all four consumers using symbolic links. It builds the local CLI with channel `dev`, packages with `--publish=never`, and verifies the final AppImage and native modules. It does not run git, upload, install, or launch the application. Use `-PrepareOnly` to stop after source synchronization and dependency installation. `-SourceRoot` and `-BuildRoot` select different source and managed build directories.

Artifacts are under `~/.local/state/openchamber-fork/build/openchamber/packages/electron/dist`, including `OpenChamber-<version>-linux-x86_64.AppImage`. The Windows source directories retain their own dependencies and artifacts. Linux uses the existing ordinary process-spawn and non-Windows filesystem paths; the Windows Process Broker and Recycle Bin helper are not ported.

Linux runtime validation uses a desktop-compatible, directory-scoped CoW view. It preserves original paths and reads the host's existing files, redirecting writes in the protected directories into disposable layers. Environment-variable data relocation and special synchronization of global AGENTS, skills, or credentials are not needed. This is a development environment for trusted local forks, not a hostile-application sandbox.

Start the built AppImage inside CoW:

```powershell
pwsh -NoProfile -File scripts/Start-LinuxCandidate.ps1
```

For a timed startup check, use `-SmokeSeconds 30`. This requests termination after 30 seconds and allows another 10 seconds before force termination. It is a smoke run, not an assertion that every application feature is ready. `-BuildRoot` selects the managed Linux build directory. The launcher requires a systemd user session, a local Wayland or X11 display, and bash, zsh, or sh as the login shell. It rebuilds the environment from `systemctl --user show-environment --output=json` and runs the login shell inside CoW instead of inheriting the development agent's private service URLs, passwords, and CLI overrides. User settings in the login environment and shell configuration remain effective.

The launcher uses one `fuse-overlayfs` mount per protected directory, with bubblewrap arranging the view. `/` and the system program/library directories no longer pass through FUSE. The default protected roots are HOME, `/tmp`, `/var/tmp`, and existing `/mnt`, `/media`, and `/run/media` directories. Standard application data locations and absolute data-directory overrides are resolved, including symlinks, and overlapping roots are collapsed. A missing configured directory is protected through its nearest existing ancestor without creating it on the host. The managed build directory and launch working directory are included by the Candidate launcher.

For projects, common Git directories, or data locations outside those roots, add an absolute directory:

```powershell
pwsh -NoProfile -File scripts/Start-LinuxCandidate.ps1 -CowPath /data/projects
```

Multiple directories can be passed from PowerShell with `-CowPath @('/data/projects', '/srv/git')`. The remaining ordinary filesystem view is read-only: a missed data directory must not silently become a host write. Do not place application data under the directly shared `XDG_RUNTIME_DIR`. If a login-shell configuration introduces a data path not covered by the initial layout, the entrypoint reports it before application startup; add that directory with `-CowPath` rather than relocating the data through environment variables.

Copied ownership is represented by user xattrs; no host root privileges or recursive ownership changes are required. Each mount root receives the source directory's ownership and mode before mounting, preserving HOME permissions and `/tmp`'s sticky writable mode. Install `fuse-overlayfs`, `bubblewrap`, util-linux (`setpriv`, `mountpoint`), coreutils (`realpath`, `stat`), and `attr` (`setfattr`) before use; `fusermount3` comes from FUSE. Desktop lifecycle uses systemd's `systemd-run` and `systemctl`. `xdg-dbus-proxy` is no longer used.

Desktop mode shares the host PID, IPC, network, and hostname view. A unique systemd user scope handles remaining child processes on exit; a session lock prevents overlapping Candidate launches. Keeping host PID identity is important: Electron passes its PID to desktop services such as systemd. Candidate's AppImage is extracted inside CoW with extraction output suppressed, then AppRun starts normally. There is no tray proxy, PID handshake, or stdin startup gate. Normal exit is available from the tray menu's **Close** item, even when closing the window only hides it. Non-desktop filesystem tests retain a private PID namespace for cleanup.

Each invocation starts fresh layers under `~/.local/state/openchamber-fork/cow/<run>/` (override the parent with `-SandboxRoot`). `layout.json` records the actual source roots, shared interface paths, and lifecycle scope, without environment values or credentials. Numbered directories contain `upper/` (retained changes), `work/` (internal metadata), and `merged/` (mounted only while running). The storage directory is hidden from the application's view. Keep the upper layers private: they can contain copied credentials and application data. Do not edit or delete a run while mounted. After normal exit, confirm each `Merged` path listed in `layout.json` is no longer a mountpoint before removing that run. Layers are not automatically reused or merged back into the host; older run directories retain their original layout.

CoW boundaries and current limitations:

- Desktop mode directly shares `/run/user/<uid>`, `/dev/shm`, X11 sockets when present, `/dev/dri`, NVIDIA device nodes, and `/dev/snd`. Existing SSH/GPG agent sockets and an absolute Wayland socket outside the runtime directory are also passed through. This covers the standard display, graphics, audio, input-method and authentication interfaces without per-application permission patches. These are shared services/devices, not CoW storage.
- Session D-Bus is direct, without filtering or rewriting the address. Root-visible runtime sockets, including the system bus, remain subject to normal host OS permissions rather than a custom allowlist. Portals, notifications, keyrings and systemd can therefore work normally, but host services may modify host files or launch processes outside CoW. Merely making the filesystem mount read-only does not make socket requests read-only.
- Networking is shared, including localhost and abstract Unix sockets. Remote API calls, scheduled tasks, clipboard/compositor actions, authentication-service updates, and kernel-generated crash reports are not rolled back. External editors/file managers can see the host version of a path rather than its CoW version. Direct local writes in the listed CoW roots are isolated; host-mediated side effects are not.
- The lower filesystem is live, not a point-in-time snapshot. Files already copied into the upper layer stop following host changes. Concurrently changing multi-file databases are not guaranteed a transactionally consistent snapshot.
- The local CLI remains on channel `dev` and normally uses `opencode-dev.db`; an official stable CLI normally uses `opencode.db`. CoW does not rename or synchronize these databases. An empty Fork session list can therefore be expected even when the managed CLI starts successfully. To seed the Fork database, explicitly back up the stable database with SQLite after closing the applications; the launcher never does this automatically.
- Chromium's three transient `Singleton*` entries are removed only in the CoW profile view so Candidate does not reuse the host application's single-instance lock. No global settings, skills, or authentication files are specially synchronized.

To discard retained CoW runs after closing Candidate (Stable can remain open):

```powershell
pwsh -NoProfile -File scripts/Clear-LinuxCow.ps1 -WhatIf
pwsh -NoProfile -File scripts/Clear-LinuxCow.ps1
```

Use `-SandboxRoot` if a custom storage directory was used. Cleanup keeps the storage parent, validates run directory names, and refuses unexpected entries, active mounts, or an occupied desktop Candidate lock. It never stops applications or forces unmounts.

Run the filesystem regression checks without launching Electron:

```powershell
pwsh -NoProfile -File scripts/Test-LinuxCow.ps1
```

Run the visible Candidate tray integration check:

```powershell
pwsh -NoProfile -File scripts/Test-LinuxCandidateTray.ps1
```

It identifies only the native tray connection belonging to its own launcher's descendant, exercises Hide Window, Show Window, and Close through the actual exported tray menu, and verifies normal exit, deregistration, scope cleanup, and unmounting. The timed fallback is only failure cleanup, not a substitute for a passing tray exit.

The checks exercise tmpfs, the home filesystem, and the source filesystem: original reads, root-directory creation, copy-up writes, deletion, rename, symlink writes, child writes, detached-child cleanup, host permissions, retained upper files, fresh-layer behavior, unmounting, and exit-code propagation. They also check that an ordinary socket in CoW is isolated while a host runtime socket remains directly usable. Add `-Desktop` to `Test-LinuxCow.ps1` to verify shared runtime writes and systemd-scope cleanup. The visible AppImage tray lifecycle is tested separately. Full input-method, audio, remote SSH and in-app upgrade testing remain separate checks.

Follow-up: the early server dependency preload is a Windows-specific startup-speed optimization. It can be disabled on Linux in a later change. The initial Linux build retains the preload and its environment-access audit, but preloads only the SDK client entrypoint to avoid importing the SDK process-launch dependencies before the shell environment is ready.

### Windows Candidate

Candidate starts from the unpacked Electron executable inside the configured Sandboxie box. Sandboxie redirects its writes to a copy-on-write sandbox, which keeps default Candidate state separate from the installed Stable application. The sandbox files remain visible to the host by design so they can be inspected or removed between tests. This is not a virtual machine or a security boundary against the host user.

Sandboxie resource rules and filesystem links may be configured manually to share selected host paths. That is an explicit user choice. OpenCode's ordinary `OPENCODE_CONFIG*` variables and OpenChamber's `OPENCHAMBER_DATA_DIR` remain supported product overrides and are intentionally inherited rather than scrubbed by the launcher.

## Manual Upstream Rebase

Upstream rebases are decision-heavy maintenance and are always performed manually. No coordinator script fetches or rebases either repository. Rebase OpenChamber first, resolve its conflicts and semantic changes, then treat the exact root `@opencode-ai/sdk` dependency as the compatibility baseline for the OpenCode migration.

Run `scripts\Get-UpstreamRebaseContext.ps1` to inspect local branches, worktree states, the OpenChamber target branch, the pinned SDK version, and the expected OpenCode release tag. The command is read-only and never fetches missing refs.

## Coupled Development

1. Use the normally installed release package as Stable. Do not start the development conversation from Candidate.
2. Make the OpenCode API change and regenerate the client contract and legacy JavaScript SDK.
3. Run `scripts\Sync-OpenCodeSdk.ps1` to stage and junction-link the local SDK into every OpenChamber consumer.
4. Adapt OpenChamber and run focused source checks.
5. Run `dotnet .\scripts\ForkCoordinator.cs -- build-candidate`.
6. Run `scripts\Start-OpenChamberCandidate.ps1`.
7. Validate the packaged Candidate inside Sandboxie. Discard or inspect its Sandboxie data as needed; do not promote it.

`build-candidate` regenerates and links the SDK, builds the local OpenCode CLI with channel `dev` and without its unused embedded Web UI, packages OpenChamber, then verifies the versions of the unpacked `OpenChamber.exe` and bundled `opencode.exe`. The Windows bundled CLI uses the GUI PE subsystem because it is an internal non-interactive child with redirected standard handles; the packaged verifier rejects a console-subsystem artifact. Candidate runs that bundled CLI normally, without an external OpenCode server or an injected runtime profile. The launcher uses Sandboxie's `/wait` mode and returns after Candidate exits.

Windows `build-candidate` and `build-release` use `E:\OpenChamber\bun-v1.4.2-release\bun.exe` and verify its version before starting the build. The coordinator sets `PATH`, `npm_execpath`, and `OPENCHAMBER_OPENCODE_BUN_RUNTIME` so SDK generation, packaging, and bundled OpenCode compilation use that same runtime.

`scripts\Sync-OpenCodeSdk.ps1` regenerates the OpenCode client outputs, builds the legacy `@opencode-ai/sdk`, stages a publish-shaped package under `generated\opencode-sdk`, installs its production dependencies, and replaces each consumer SDK with an NTFS junction. A partial staging or junction failure triggers a root frozen-lockfile reinstall. `scripts\Restore-OfficialOpenCodeSdk.ps1` removes the local link by restoring dependencies from the frozen lockfile.

## Release Packaging

Installable packaging is separate from Candidate testing. It requires clean OpenChamber `local` and OpenCode `dev` worktrees and a matching SDK version. Run:

```powershell
dotnet .\scripts\ForkCoordinator.cs -- build-release
```

The builder regenerates and links the SDK, runs workspace type-check and lint, bundles OpenCode from local source with channel `dev` and the Windows GUI PE subsystem, packages with `--publish=never`, verifies the packaged CLI subsystem and `opencode-dev.db` default path, validates `latest.yml`, and atomically writes the installer, blockmap, update manifest, and `release.json` under `E:\OpenChamber\release`. It never uploads or installs a package. Install the validated package normally to update Stable.

## Windows GitHub Release

GitHub Draft creation is a separate, explicit step after `build-release`. The dedicated script targets `AlanIWBFT/openchamber`, mirrors the title and release notes from the matching `openchamber/openchamber` release, and uses Markdig source spans to strip literal `@` markers from ordinary Markdown text that can contain GitHub user or team mentions. Markdig-recognized code, explicit Markdown links, and angle-bracket CommonMark autolinks are left untouched; common embedded forms such as email addresses and `git@github.com` are preserved; and the original Markdown is edited in place rather than rendered again. Release notes containing an `@` marker in a raw HTML block, in any Markdown paragraph that also contains inline raw HTML, in mathematics, in an HTML entity, or in a bare `scheme://` or standalone `www.` URL fail before Draft creation instead of being guessed at. The script uploads only the installer, blockmap, and architecture-appropriate Windows updater manifest from one immutable local release directory. `release.json` is used to revalidate the local files but is not uploaded.

Create or resume the Draft:

```powershell
dotnet .\scripts\Publish-OpenChamberRelease.cs -- E:\OpenChamber\release\OpenChamber-1.19.0-win-x64
```

Run the focused release-note sanitizer regression suite:

```powershell
dotnet .\scripts\Test-ReleaseNotesSanitizer.cs
```

Relative release-directory arguments are resolved from the current working directory, not from the coordinator directory.

Before changing GitHub, the script validates the local release and requires `gh` to be authenticated as `AlanIWBFT`, the target repository to remain public, and the exact OpenChamber source commit recorded in `release.json` to already exist in `AlanIWBFT/openchamber`. It never pushes source. It creates or resumes a matching Draft, pins the Draft target to that exact commit, rejects extra or mismatched assets, and verifies GitHub's uploaded SHA-256 digests. A failed upload intentionally leaves the validated Draft resumable by the same command. The script never publishes the Draft or changes Latest; review and publish it manually on GitHub, which creates the release tag if it does not already exist.
