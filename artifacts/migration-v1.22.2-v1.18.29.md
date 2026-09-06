# Fork Migration: OpenChamber 1.22.2 / OpenCode 1.18.29

Research and implementation record: 2026-09-06. Original baselines and the approved plan are retained below; execution results follow the validation plan.

## Targets and Original State

| Repository | Local branch and HEAD | Previous base | Target tag |
| --- | --- | --- | --- |
| OpenChamber | `local`, `d3f4a993a` | `v1.22.1`, `13805e5ce` | `v1.22.2`, `5012de6b8` |
| OpenCode | `dev`, `12267912c7` | `v1.18.28`, `22006d9765` | `v1.18.29`, `16747470f9` |

- Both official `origin` remotes were fetched with tags.
- OpenChamber's target root, UI, web, and VS Code manifests pin `@opencode-ai/sdk` to `1.18.29`. This determines the backend target; do not substitute the newer development branch.
- The OpenCode target tag's parent is `02a167e048`; the previous tag's parent is `20a7743876`. Release commits branch from feature history. Rebase onto the tag itself, excluding the previous release commit from the replay range.
- `openchamber-official` and `opencode-official` were clean and their `official` branches were reset with `--keep` to the target tags. The OpenCode reference branch still has its existing upstream tracking configuration; its ahead/behind display is not the migration target.
- Both primary worktrees were clean at the original HEADs above before implementation. Each repository now retains its original HEAD in the non-overwriting backup branch `migration-20260906`.

## Scope and Decisions

- Preserve the final functionality represented by every `Local:` commit: 43 OpenChamber commits and 38 OpenCode commits. Intermediate implementations superseded by later local commits are not separate features to resurrect.
- Prefer rebase and small adaptations at existing ownership boundaries. Do not use this upgrade to redesign unrelated modules or migrate to the parallel native V2 runtime.
- Stop and ask the user, one question at a time, when a meaningful product or architecture choice appears. Reassess previous choices if implementation evidence changes their consequences.
- Do not run a full build, package an application, build or launch Candidate, publish, push, or install a release.
- Composer Up/Down recall adopts upstream's independent persisted input history, including its timestamp ordering, session/global scope, original submissions, and attachment recall. The transcript-derived input still uses the local visible-message boundary. Transcript order and undo/redo retain authoritative sequence semantics.
- Missing-directory recovery follows upstream. External deletion can destroy the execution environment; do not attempt to guarantee or recover inherently unrecoverable execution state. Earlier proposals for busy-session move guards, idle-lane release notifications, migration locks, and shell state handoff were explicitly withdrawn. Preserve ordinary local message-cache correctness without introducing those mechanisms.
- Goal Mode's new consecutive-output-truncation rule uses local authoritative sequence order to select the preceding completed non-summary assistant reply. Retain upstream error classification, the two-truncation bound, explicit resume behavior, and Goal creation-time boundary. No new persisted counter or Goal subsystem redesign.
- Tests require `bun install --frozen-lockfile` first. Compare failures with the same test on the target official worktree; failures also present upstream are recorded, not repaired as part of this migration.

## Consumed Paths

OpenChamber imports the legacy package through `@opencode-ai/sdk/v2` and related client exports. Its packaged local CLI comes from `packages/opencode`, not a replacement native V2 CLI. That legacy runtime imports selected Core, Schema, and LLM implementations through their export mappings.

Relevant ownership anchors:

- OpenChamber `packages/ui/src/sync/DOCUMENTATION.md`, `stores/DOCUMENTATION.md`, and `components/chat/message/parts/DOCUMENTATION.md`.
- OpenChamber `packages/electron/README.md` and web module documentation for Git, OpenCode lifecycle, event streams, agent tools, and the control service.
- OpenCode `packages/opencode/README.md`, `specs/v2/unified-exec.md`, `specs/v2/plan-mode.md`, `packages/opencode/todo-card-reset-spec.md`, and OpenAI compaction specifications.
- OpenCode `packages/opencode/src/plugin/openai/README.md` documents the current WebSocket continuation and fallback contract.
- Coordinator `README.md` distinguishes manual upstream rebasing from coupled API feature development. Its manual rebase workflow starts with OpenChamber, then the backend matching its SDK pin.

All official commit subjects and changed-path inventories in both tag intervals were surveyed. Runtime implementations and integration points affecting the consumed paths were inspected. Historical changelog prose, translations, hosted console functionality, and extension-only changes were classified rather than subjected to a full independent product review.

## OpenCode Upstream Delta

The interval contains the release commit, release-version synchronization, two Codex filters, a hosted-console quota-reset support action, and Chinese enterprise-documentation formatting.

| Upstream change | Migration consequence |
| --- | --- |
| `500c46ec79`: integer GPT versions in Codex filtering | Already backported locally; retain the upstream implementation and tests instead of replaying the duplicate feature. |
| `02a167e048`: compare GPT major and minor versions | Already backported locally; retain upstream. Other local changes in `codex.ts`, especially WebSocket diagnostics, remain. |
| `5cf9f517cf`: hosted console support quota reset | Not imported by the packaged CLI. It does not replace local provider quota diagnostics or retry policy. |
| `5d5c35ee71`: Chinese Markdown spacing | Documentation-only, no local behavior replacement. |
| Version synchronization and `v1.18.29` release | Adopt target versions and lockfile workspace metadata while retaining local dependency patches. |

No new upstream change in this interval replaces persistent exec lanes, durable local message/part order, atomic import, one-time order repair, managed lifecycle, Windows process broker, Recycle Bin handling, native compaction, or provider recovery. Avoid rewriting them merely because the repository also contains native V2 implementations.

## OpenChamber Upstream Delta

| Change family | Effect on the local fork |
| --- | --- |
| Persisted composer history; session default; attachment recall | Adopt the new navigator and store. Adapt the transcript hook's result shape without restoring ID-based revert filtering. |
| Local slash-command planning and queued context delivery | Keep upstream action/prompt separation and failure restoration. Local `/undo`, `/redo`, and `/compact` continue calling their existing authoritative implementations. |
| Enter-to-send preference; IME handling; mobile send/queue action | Keep upstream key policy and settings persistence. No new local keyboard behavior is required. |
| Missing-directory recovery and archived restore | Keep tri-state filesystem probing, primary-project destination, partial-result reporting, and shared-directory retention. Adapt only local cache/order ownership. |
| `OPENCHAMBER_CHATS_DIR` | Keep server-owned root resolution and startup warming. Honor explicit environment settings; do not introduce coordinator-specific runtime roots. |
| Child-session pagination and overlapping-pull exclusion | Keep upstream fix for more than 200 sessions. Compose with local stale-response, deletion, reconnect, and authoritative-state guards. |
| Project-action terminal execution and cross-client reconciliation | Keep purpose/execution metadata, command-mode terminals, pending-create cancellation, canonical directory keys, chunk replay, and demand-scoped polling. These are UI PTY terminals, not replacements for agent exec lanes. |
| Async PTY spawning | Compose upstream `await provider.spawn(...)` with local shutdown checks before admission and after spawn resolves. A late-created process must be cleaned up rather than published after shutdown. |
| Goal output truncation recovery | Keep upstream bounded continuation. Adapt only predecessor selection to sequence order, as explicitly approved. Summary markers from local native compaction remain excluded from consecutive agent-reply checks. |
| Large tool diff limits and surrogate-safe preview truncation | Keep the bounded preview before loading the heavy viewer. Preserve local unified-exec output materialization and tool presentation independently. |
| Persisted JSON output view mode | Merge upstream setting with local code-block-wrap persistence; do not overwrite either setting's validation/autosave chain. |
| Settled Markdown table sizing | Keep upstream. Verify local bottom-follow behavior with late layout changes and with a user who has scrolled away. |
| File stat jitter and fresh content polling | Keep upstream stat threshold, cache bypass, and confirmed content reads. No local Git worker redesign is implied. |
| Custom-provider metadata preservation and XDG paths | Keep upstream provider-model metadata merges and common config-root resolution. Preserve local environment probing and defer env-sensitive module imports until the snapshot is applied. |
| Windows Bun executable/shim resolution | Use upstream resolver in its development/test launch paths. Preserve explicit selected Bun runtime and compile-target behavior in local packaging; they solve different problems. |
| Update notes and per-release changelog sources | Adopt `changelog/index.json` fetching/rendering. Keep local fork updater feed and installer behavior. Notes were already read from official upstream, so their format change does not justify a new notes-source policy. |
| Test race fixes | Keep official relay-listener and walkthrough synchronization fixes together with local Bun 1.4/native-promise test adaptations. |
| VS Code comments, permissions, localization, and path matching | Surveyed for shared UI/SDK effects. Retain upstream. Do not extend local backend migration to unrelated extension-host implementations. |
| Docker dependency patches and UTF-8 locale; Linux lingering warning | Retain upstream; no local Windows runtime replacement. Do not run their builds. |
| Release tooling, changelog history, docs, translations | Retain upstream generated/source organization. No release preparation or publication is part of this task. |

## Concrete Adaptation Boundaries

### Message Caches on Directory Moves

`packages/ui/src/sync/session-actions.ts::reconcileSessionMove` currently copies message and part arrays between directory stores. Local `message-order.ts::decodeStoredMessageRecords` removes `seq` from those objects and keeps it in a store-keyed side index. Copying arrays alone therefore cannot transfer the ordering authority.

The adaptation must handle the affected session's message/part sequence indexes and invalidate outstanding snapshots at both locations. Prefer existing snapshot/order APIs; if a complete valid cache cannot be transferred, invalidate that session's materialization and reload it through the existing authoritative loader. Do not invent sequence values, reset unrelated sessions, or turn this into shell execution recovery. Verify successful moves and upstream's partial-move results.

### Goal Mode

`packages/web/server/lib/session-goal/runtime.js::hasRepeatedLengthTail` chooses the predecessor using `time.created`. A concrete conflicting history is: A truncated at seq 10 / time 300, B normal at seq 11 / time 200, C truncated at seq 12 / time 400. Upstream chooses A as C's predecessor; the local contract says B.

Use the authoritative message sequence to establish adjacency among completed non-summary assistant replies. Test both false-positive and false-negative truncation chains, summary messages between turns, and explicit resume. Keep the existing Goal age boundary rather than adding another sequence checkpoint.

### Terminal Shutdown

`packages/web/server/lib/terminal/runtime.js::spawnPty` has local shutdown admission and post-spawn disposal checks. Upstream makes provider spawning asynchronous and adds cancellation of pending creates. Preserve both, including cleanup after a delayed spawn resolves. This maintains an existing local lifecycle guarantee; it is unrelated to external worktree deletion.

### Startup and Git

Keep Electron's environment-probe worker, overlapping startup, bounded dependency preload, and import-time environment guard. The new update-notes module is import-safe: its network operation occurs only when called. New config-root environment reads must stay in the post-probe graph.

Keep the four persistent Git read Worker Threads and Windows process broker. Upstream's Git service delta here adds `prunable` worktree metadata; it does not replace the local latency-sensitive status/PR/diff execution path. Preserve the latest v1.22.1 Git-status adaptation rather than restoring an earlier worker protocol.

## Local Feature Ledger: OpenChamber

All rows retain final behavior, with adaptations above where applicable. Commit subjects below omit the common `Local:` prefix.

| Commit | Subject / migration intent |
| --- | --- |
| `d3f4a993a` | fix migrated UI lint diagnostics; retain relevant corrections, do not mass-reformat upstream |
| `8320459a3` | adapt Git status migration to v1.22.1; preserve final status API integration |
| `6fba539dc` | keep new chat output at the bottom |
| `04913423b` | use native promises in Bun git tests |
| `ea02ad329` | adapt migrated tests to Bun 1.4 |
| `c03cc8d6a` | preload server dependencies during shell discovery |
| `01a1fe74d` | add server preload environment guard |
| `f494a3175` | Lazy-load optional server dependencies |
| `001273174` | move Windows env probing to worker thread |
| `8a477010b` | overlap Windows env probing with startup |
| `cad1a90af` | make Electron own Windows env probing |
| `c6baf688b` | support explicit Bun packaging runtime |
| `996e90267` | adapt message materialization to OpenChamber v1.20.0 |
| `711359249` | route Windows Git through process broker |
| `6650ae6e5` | run OpenCode build with selected Bun |
| `77ab741f4` | bundle GUI subsystem OpenCode |
| `217b83d52` | Skip redundant desktop port persistence |
| `cb725dff6` | wait for managed startup migrations |
| `650a3afbe` | Move latency-sensitive Git reads off main thread |
| `cee39f7e9` | use patched Bun for bundled CLI; retain default/explicit runtime selection, do not hard-code a new runtime |
| `a1c1d3a1f` | build(electron): package Recycle Bin helper |
| `2009fedc2` | Enable memory visibility by default |
| `37bd3ea20` | Point desktop updater at fork releases |
| `ceca4a4df` | adapt agent tool contract to OpenChamber v1.19.0 |
| `c0d885250` | preserve question answers in session messages; synthetic answer IDs are not fork boundaries |
| `e3446c35f` | bound revert history loading |
| `f76b67627` | adapt OpenChamber to unified exec lanes |
| `3db4f12ec` | Confirm before quitting active conversations |
| `93c2b3fa4` | harden OpenChamber agent tool contract; typed request envelope and shared validation |
| `f6acd19c4` | disable NSIS desktop shortcut |
| `44f50dcc7` | coordinate desktop shutdown |
| `632d73b88` | test(ui): preserve sequence semantics in history fixtures; distinguish transcript tests from approved input-recall behavior |
| `6be066a74` | fix(electron): target bundled OpenCode builds explicitly |
| `92d7f5cf4` | fix(web): back off upstream event reconnects |
| `598a0dde2` | feat(ui): show actionable provider errors |
| `1fde5b8d6` | fix(sync): own SSE reconnect recovery |
| `be5b8b91f` | feat(electron): bundle local OpenCode Candidate backend; preserve packaging contract without building it |
| `40fef9e59` | fix(ui): persist shared settings and rename ownership |
| `62c86b55a` | fix(electron): stabilize desktop lifecycle; preserve Vite-only Electron development path |
| `67a6b442d` | fix(ui): respect disabled custom question answers |
| `d2daab9a9` | feat(ui): enforce authoritative session state |
| `7c42af224` | feat(ui): refine activity and tool presentation |
| `a4437dd5c` | feat(ui): present unified exec sessions |

## Local Feature Ledger: OpenCode

| Commit | Subject / migration intent |
| --- | --- |
| `12267912c7` | align session tool test registry mock |
| `a346bba006` | backport Codex GPT major and minor comparison; absorbed by upstream `02a167e048` |
| `5c2e503e2d` | backport integer GPT versions in Codex model filter; absorbed by upstream `500c46ec79` |
| `af561d926a` | handle premature readable closure; retain Effect NodeStream dependency patch and broker regression coverage |
| `4a397aa8b3` | keep OpenCode builds lockfile-clean |
| `519837fbd0` | chore(sdk): regenerate migrated contracts; regenerate from final target sources, not by hand |
| `44a06980ab` | adapt provider recovery to OpenCode v1.18.21 |
| `003b833b86` | add Windows process broker |
| `068672de5e` | add GUI subsystem build option |
| `3b9a113e7a` | report database migration lifecycle |
| `9246283e0c` | make message order repair one-time |
| `17416397b1` | make session import atomic |
| `7f090663ed` | support explicit Bun compile runtime |
| `d0664f1b0c` | feat(opencode): diagnose Recycle Bin blockers |
| `b902756bce` | isolate PowerShell lane history |
| `fb7fc69ab1` | add OpenAI WebSocket continuation and size fallback |
| `55ba26677b` | move OpenAI compaction to AI SDK; final implementation supersedes earlier native compact transport |
| `92f31d86e6` | feat(opencode): support OpenAI native compaction; preserve checkpoint/replay behavior, not superseded transport |
| `21931b7961` | add managed shutdown protocol |
| `68ce9e678c` | preserve authoritative session ordering |
| `83291003ce` | stabilize unified exec lane test |
| `4a68fd73d8` | consolidate unified exec lanes |
| `6e2473b0d3` | preserve provider recovery and native error diagnostics |
| `aa7e4477bc` | refine iterative review guidance |
| `57c2cfd1e9` | fix(opencode): preserve apply patch line endings |
| `de304020ed` | feat(opencode): select explicit build targets |
| `4542cd367b` | fix(opencode): preserve user language in reviews |
| `1db7915807` | fix(session): preserve Chinese compaction language |
| `1b15c8ce16` | feat(plan): recover plan_exit and restore build handoff |
| `515e32c9be` | docs(opencode): document git editor safeguards |
| `07e97ac716` | fix(session): match Windows directory separators |
| `576d494507` | fix(tool): read PowerShell stdin as UTF-8 |
| `7ab6d5bbb8` | fix(tool): route PowerShell deletes to recycle bin; no permanent-delete fallback |
| `934c81bb18` | fix(tool): improve shell command execution and guidance |
| `6f67e807db` | fix(tool): skip extra external_directory prompts for read-only file tools |
| `0d32dfcb69` | feat(opencode): prioritize system ripgrep and tweak parameters |
| `de7ee0a4f3` | feat(opencode): enable Exa search and code tools by default |
| `d1b30893b2` | fix(opencode): clear live todos at new turn start; historical todo cards remain |

Native compaction's current AI SDK path keeps opaque checkpoints, retained user history and the tail, and avoids synthetic continuation prompts for successful native checkpoints. Preserve the local OpenAI dependency patch. WebSocket fallback must not silently replay a partially delivered transport response. Provider-level retry has its own existing policy, including explicitly accepted possible replay after partial progress; do not confuse the two layers or silently change that policy.

## Execution Plan After Approval

1. Recheck all worktree states and target refs. Create non-overwriting backup refs for the two original local HEADs. Preserve unrelated work; do not stash or discard it implicitly. Read each target repository's current instructions and all required implementation skills before edits.
2. OpenChamber phase: rebase only `v1.22.1..local` onto `v1.22.2`, using `git rebase --onto v1.22.2 v1.22.1 local`. Resolve textual conflicts against final local behavior and the decisions above. Do not interpret a missing custom SDK field as permission to remove its consumer. Coupled type checking waits for the regenerated backend SDK.
3. OpenCode phase: rebase only `v1.18.28..dev` onto `v1.18.29`, using `git rebase --onto v1.18.29 v1.18.28 dev`. Explicitly account for the two verified duplicate Codex commits; do not assume Git will always drop them automatically, and do not skip any broader commit merely because a hunk looks upstream-owned.
4. Install dependencies with `bun install --frozen-lockfile` at each repository root before its tests. Keep local dependency patches and adopt target package versions. Any install/lockfile failure is investigated, not bypassed by silently removing `--frozen-lockfile`.
5. Regenerate the client and legacy SDK from final OpenCode sources. The coordinator's `scripts/Sync-OpenCodeSdk.ps1` already runs `bun run --cwd <opencode>/packages/client generate`, removes the generated SDK tsbuildinfo cache, runs `bun run --cwd <opencode>/packages/sdk/js build`, stages publish-shaped output, and junction-links every consumer. Use it after both repositories pin `1.18.29`; avoid duplicate generation or linking SDK source directly. This is contract generation/SDK compilation, not a full application build.
6. OpenChamber adaptation phase: finish the input-history hook, directory-cache sequence handling, Goal predecessor selection, terminal shutdown composition, and any actual conflict fallout. Retain final Git/startup/lifecycle contracts. Update owning docs and tests only for real contract changes.
7. Run focused checks below, compare failures against clean target official worktrees with their own frozen installs, then review direct regressions. Official OpenChamber uses its official SDK; only the migrated local tree uses the local coupled SDK.
8. Compare the final patch series against the two original local ranges using `git range-diff`, and check both final `git diff <target-tag>..HEAD` results against this ledger. Explain intentional duplicate removal and implementation changes. Stop at source and focused-validation results; no application build or promotion.

Use non-interactive Git commands and command-scoped editor suppression when continuing a rebase. Do not amend, squash, create extra commits, or push as an implicit cleanup; agree any necessary history operation with the user. Record the final disposition of each ledger row.

## Focused Validation

This table records planned coverage areas, not a claim that every suite was run. Actual commands and results are recorded in Execution Results below.

| Area | Focused coverage |
| --- | --- |
| Backend contracts | Legacy HTTP API session/global/SDK tests; stored seq fields, question payloads, retry resolution, stop boundaries; generated client/SDK output and consumer resolution |
| Backend state | Message pagination, revert/compaction, atomic import, one-time migration/repair, prompt and shell todo reset |
| Agent execution | Unified exec lanes, cancellation/EOF/reset, original tool-output updates, PowerShell UTF-8/history, Recycle Bin failures, Windows broker stream closure |
| Providers | Codex model filter, provider error/retry, processor recovery, AI SDK/native compaction and OpenAI WebSocket continuation/fallback |
| UI authority | message-order, materialization, session-message-loader, session-actions, event pipeline/reducer, reconnect recovery, revert/history fixtures, child discovery |
| New integrations | Input history/navigation and deletion cleanup; directory relocation with seq indexes; Goal truncation sequence; terminal pending spawn during shutdown; settings persistence |
| Presentation | Unified exec root/follow-up cards, provider errors, disabled custom question answers, bottom-follow with settled Markdown layout and user scroll-away |
| Startup and Git | Environment probe/preload guard, lifecycle shutdown, migration readiness, four-worker timeout/cancellation, broker routing, status-task API and `prunable` parsing |
| Tool and release contracts | Agent-tool envelope/control question answers; architecture/PE-subsystem/updater-feed unit tests; update-notes parser |

- Use package scripts as the source of truth. Backend type checks run with `bun typecheck` from the relevant package directories; do not run backend tests from the repository root.
- OpenChamber uses package `type-check` scripts and focused Bun/Node test invocations according to each test's imports. Preserve process isolation where module mocks require it.
- Run changed-path lint; run dead-code checks when import/export shape or file ownership changes. Do not use `release:prepare`, `package`, or build launchers as shortcuts for checks.
- The OpenCode package's generic `test` script prepares the Windows Recycle Bin helper first. Do not accidentally invoke a build through that wrapper. Focused direct tests must use existing suitable helper artifacts; if absent, report the limitation and ask before expanding build scope.
- Monitor any running Bun process's cumulative CPU every 60 seconds. Terminate a confirmed hang; increasing CPU alone is not proof of test progress.
- For failures of tests that exist upstream, run the same invocation in the target official worktree. For new/local-only cases without an upstream equivalent, state that limitation rather than treating a nonexistent official test as a pass.
- Source checks cannot validate the packaged PE artifact, installer, actual updater, Sandboxie behavior, or full startup performance. Those remain explicitly unvalidated without a later requested build and runtime exercise.

## Execution Results

- OpenChamber `local` was rebased onto `v1.22.2`; replayed HEAD is `974ef6ece`. All 43 original local commits remain represented, in order.
- OpenCode `dev` was rebased onto `v1.18.29`; HEAD is `dbb25ecf70`. Its 36 retained local commits match the original series, with only the Codex test insertion context changing. The verified duplicate backports `5c2e503e2d` and `a346bba006` were explicitly dropped; their functionality and tests come from upstream. This accounts for all 38 original ledger entries.
- Conflict resolutions preserve upstream composer action planning and independent history, JSON view persistence, async terminal spawning, Bun resolution, and update notes alongside local authoritative state, exec presentation, error reporting, lifecycle, and preload behavior.
- Nine OpenChamber files received additional adaptations and regression tests: directory-cache sequence transfer with scoped snapshot invalidation, Goal predecessor selection by seq, async PTY post-shutdown cleanup coverage, and local test fixture/mock adaptation. These were initially left uncommitted, then recorded in the three user-authorized commits below. No amend, squash, push, or publication was performed.
- Directory moves use an explicit order-preserving loader invalidation mode, then transfer sequence indexes with cached records through the existing transactional sequence API. Source indexes are removed; both session snapshots expire; absent source caches do not erase destination ordering. This adds no execution recovery, busy guard, or shell handoff.
- `bun install --frozen-lockfile` succeeded in both primary repositories and in `openchamber-official` before its comparison tests. Bun version was `1.4.0`.
- `scripts/Sync-OpenCodeSdk.ps1 -SkipTypeCheck` generated the client, compiled the legacy SDK, and linked publish-shaped SDK `1.18.29` into all four consumers. Subsequent type checks ran separately. Regeneration left no tracked OpenCode SDK differences.

### Passed Checks

| Scope | Command / result |
| --- | --- |
| OpenCode types | `bun run typecheck` in `packages/opencode`: passed |
| OpenChamber types | `bun run type-check:ui`, `bun run type-check:web`, `bun run vscode:type-check`: passed |
| Codex | `bun test ./test/plugin/codex.test.ts --timeout 30000`: 46 passed |
| Backend API and state | `bun test ./test/server/httpapi-sdk.test.ts ./test/session/messages-pagination.test.ts ./test/session/revert-compact.test.ts ./test/session/compaction.test.ts ./test/tool/unified-exec.test.ts --timeout 30000`: 220 passed, 1 skipped, 0 failed |
| Sync and chat | `node ../../scripts/run-isolated-tests.mjs src/sync src/components/chat/message src/components/chat/lib/scroll src/components/chat/composer/state/__tests__` from UI: 79/79 files passed |
| Composer history and settings | Separate Bun runs of `inputHistory.test.ts`, `useInputHistoryStore.test.ts`, and `persistence.test.ts`: 7 + 24 + 47 passed |
| Goal and terminal | Vitest runs of `server/lib/session-goal/runtime.test.js` and `server/lib/terminal/runtime.test.js`: 54 passed, 1 known upstream failure (below); both new sequence cases and delayed-spawn cleanup passed |
| Electron startup | Node tests for dependency preload, preload environment guard, startup single-flight, and startup URL selection: 15 passed |
| Quit page | Correct Bun runner for `packages/electron/quit-page.test.mjs`: 4 passed |
| Static hygiene | ESLint on the seven post-rebase changed code/test files and `git diff --check`: passed |

### Failure Comparisons and Limits

- The terminal test `lists available shells and uses the selected shell for create and restart` expects Auto to support login shells. On this Windows environment it reports `supportsLogin: false`. The same two-file Vitest invocation on clean `openchamber-official` at `v1.22.2` reproduces that exact failure (50 passed, 1 failed). It is not changed.
- Official `session-actions.test.ts` passes all 97 tests. Migrated failures were real test-integration issues: the new whole-loader mock hid the real loader required by local bounded-revert tests, and a new ambiguous-confirmation response fixture lacked local stored sequence/ownership fields. The mock was removed, its necessary runtime API stub supplied, and the fixture corrected. The final isolated sync run passes.
- An initial combined Bun UI invocation demonstrated cross-file module-mock pollution; final UI results use the repository's isolated runner. An initial Node invocation of `quit-page.test.mjs` used the wrong runner for its `bun:test` import; it is rerun with Bun, not treated as a product regression.
- Bun CPU samples increased during the long backend run, and output advanced through API, pagination, revert, compaction, and exec suites. The run completed normally in about 311 seconds.
- No full application build, packaging, Candidate launch, installation, updater exercise, Sandboxie validation, or real-browser runtime check was performed. Source/unit checks do not establish packaged runtime or end-to-end visual correctness. Not every unchanged local-only subsystem test was rerun.

### Commit Disposition

On the user's subsequent request, the nine adaptation files were committed as three focused additions rather than folded into the original feature commits. Each adapts or tests behavior introduced by the new upstream baseline; separate commits keep that dependency explicit without rewriting the validated replayed history.

| OpenChamber commit | Scope |
| --- | --- |
| `9502bf2b9` | Local: preserve sequence state across directory moves; includes owning docs, loader coverage, and migrated sync fixtures |
| `c73d01773` | Local: use sequence order for Goal truncation recovery; includes docs and reversed-timestamp tests |
| `9332e0183` | Local: test delayed terminal spawn during shutdown |

OpenChamber's resulting HEAD is `9332e0183`; OpenCode remains at `dbb25ecf70` with no additional source changes. The committed source content is the same content validated above. Commit-time checks cover staged whitespace and file scope; no tests or builds were repeated solely to record these commits.

This report is committed separately in the coordinator. Pre-existing ETL analysis tools, their README changes, and browser/scroll diagnostic artifacts are unrelated to the migration and are deliberately excluded, without removal or modification.
