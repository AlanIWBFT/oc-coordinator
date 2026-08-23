# Local OpenChamber and OpenCode Fork Workflow

This directory coordinates coupled local-fork API development without modifying either repository's machine-specific configuration.

## Prerequisites

Use PowerShell Core 7 or newer through `pwsh`, not Windows PowerShell 5.1 through `powershell.exe`. Windows packaging requires a complete Visual Studio 2022 instance with the VC++ x64/x86 tools and Windows SDK. The coordinator discovers it through `vswhere` and initializes its Developer Shell by the discovered installation path before native Electron modules rebuild.

Candidate isolation requires Sandboxie-Plus. The default launcher is `C:\Program Files\Sandboxie-Plus\Start.exe` and the default box is `OpenChamberCandidate`; both are configurable in `local-fork.config.psd1`.

## Layout

- `E:\OpenChamber\openchamber`: local OpenChamber fork.
- `E:\OpenChamber\opencode`: local OpenCode fork and source build input.
- `E:\OpenChamber\openchamber\packages\electron\dist\win-unpacked`: x64 Candidate app built from the local checkout.
- `E:\OpenChamber\openchamber\packages\electron\dist\win-arm64-unpacked`: ARM64 Candidate app built from the local checkout.
- `E:\OpenChamber\release`: atomically prepared Windows release directories.
- `E:\OpenChamber\coordinator\generated\opencode-sdk`: generated publish-shaped local SDK package used by junction consumers.

The installed release package is Stable. It is an ordinary user installation, not a coordinator-managed slot. The coordinator has no Stable, Rescue, external CLI copy, runtime-root tree, manifest, or promotion command.

## Candidate Isolation

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
