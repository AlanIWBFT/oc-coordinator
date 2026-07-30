# Local Fork Coordinator Guide

## Purpose

This workspace coordinates the local OpenCode and OpenChamber forks for coupled API work. The normal task changes an OpenCode contract or server API, regenerates the local SDK, adapts OpenChamber, and validates a packaged Candidate app in Sandboxie.

## Repositories

- OpenCode: `E:\OpenChamber\opencode`
- OpenChamber: `E:\OpenChamber\openchamber`
- Coordination scripts: `E:\OpenChamber\coordinator`

Treat the repositories as separate edit and validation boundaries. Before editing either repository, read its root `AGENTS.md`, then read every required nearest package README, `DOCUMENTATION.md`, and project skill. Apply changes to each repository only during its explicit phase.

Do not run git or GitHub commands unless the user explicitly requests them. Never commit, rebase, push, promote a Candidate, or rewrite a branch as an implicit follow-up.

## Runtime Model

- Stable is the release package installed normally by the user. The coordinator does not launch, copy, hash, validate, or promote it.
- Candidate is `packages\electron\dist\win-unpacked\OpenChamber.exe` (or `win-arm64-unpacked` on ARM64) built from the local checkout.
- `scripts\Start-OpenChamberCandidate.ps1` starts Candidate in the configured Sandboxie box, normally `OpenChamberCandidate`. Candidate must never power the conversation that is repairing it.

Sandboxie supplies Candidate's default write isolation through its copy-on-write sandbox. The sandbox directory is intentionally host-visible for inspection and disposal; it is not a virtual machine. A user may explicitly configure Sandboxie resource rules or filesystem links to share selected data. Treat that as deliberate sharing, not as the default runtime model.

Do not add custom runtime-root variables or paths. Existing OpenCode and OpenChamber data-directory overrides remain normal product behavior. In particular, do not clear `OPENCODE_CONFIG*` or `OPENCHAMBER_DATA_DIR`: an explicit user setting expresses an intent to control or share that location.

## Default Coupled Workflow

1. Use the normally installed release package for Stable work and conduct the development conversation from it, rooted at `E:\OpenChamber\coordinator`.
2. Read both repository instruction trees and inspect the actual API implementation before planning changes.
3. Modify the OpenCode contract/server implementation under the OpenCode repository rules.
4. Regenerate the current OpenCode client contract and legacy JavaScript SDK.
5. Link the generated local `@opencode-ai/sdk` into every OpenChamber consumer with `scripts\Sync-OpenCodeSdk.ps1`.
6. Modify OpenChamber against that local SDK and run focused checks in both repositories. A local CLI with the official SDK is not a valid coupled-API test.
7. Run `dotnet .\scripts\ForkCoordinator.cs -- build-candidate`. It stages a local OpenCode CLI in the unpacked Candidate app without OpenCode's unused embedded Web UI.
8. Run `scripts\Start-OpenChamberCandidate.ps1`, validate Candidate in Sandboxie, then report the result. There is no promotion step.

## SDK Contract

OpenChamber consumes the legacy package at `E:\OpenChamber\opencode\packages\sdk\js`. Public OpenCode API changes require both:

- `bun run --cwd E:\OpenChamber\opencode\packages\client generate`
- `bun run --cwd E:\OpenChamber\opencode\packages\sdk\js build`

The SDK package version must match OpenChamber's pinned root `@opencode-ai/sdk` version. The local SDK link must cover the OpenChamber root and the `packages/ui`, `packages/web`, and `packages/vscode` workspaces. Do not edit generated SDK output by hand.

The SDK build uses TypeScript composite output. The coordinator removes only the generated `tsconfig.tsbuildinfo` cache before rebuilding so a prior partial `dist` cannot be mistaken for complete output.

SDK linking is all-or-restore: build output is staged as a publish-shaped package with `dist` exports under `E:\OpenChamber\coordinator\generated\opencode-sdk`, each installed consumer SDK path is replaced with an NTFS junction to that package, and failure before all consumers resolve locally triggers one root frozen-lockfile reinstall. Never link the SDK source package directly because consumers would compile its TypeScript under incompatible tsconfigs. Never run `bun link` independently inside a workspace package because its `workspace:*` dependencies cannot be resolved as a standalone install root. Type-check failure after complete junction installation intentionally leaves the local SDK active so OpenChamber can be adapted against the new contract.

## Candidate Build

Candidate and installable-package builds bundle the CLI from `E:\OpenChamber\opencode` with `OPENCODE_CHANNEL=dev`. It stages the verified executable directly in the unpacked Electron app; it never copies an application or CLI into a slot. The OpenCode build clears its own `dist` before compiling, so the staged Candidate app, not `packages\opencode\dist`, is the runnable artifact.

## Release Contract

Installable packaging is a separate clean-source boundary owned by `scripts\ForkCoordinator.cs`. It requires clean `local` and `dev` worktrees at their current commits, rebuilds OpenCode with channel `dev`, verifies the packaged CLI's `opencode-dev.db` path, validates updater metadata, and stages immutable artifacts under `E:\OpenChamber\release`. It never uploads or installs a package. The user installs a validated package to update Stable.

Use PowerShell Core 7 through `pwsh`. Scripts and manually edited text files on this machine use CRLF line endings.
