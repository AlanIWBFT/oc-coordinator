# openchamber: v1.24.2 to v2.0.0

Local commits: 63; upstream commits: 111; changed upstream paths: 1227.
Local paths: 288; overlap: 177; absent at target: 53.

## Local commits and target path availability

### 4230eb2151 Local: feat(ui): present unified exec sessions

- present: `packages/ui/src/components/chat/ChatMessage.tsx`
- present: `packages/ui/src/components/chat/lib/turns/projectTurnActivity.ts`
- present: `packages/ui/src/components/chat/lib/turns/projectTurnRecords.test.ts`
- present: `packages/ui/src/components/chat/message/MessageBody.tsx`
- present: `packages/ui/src/components/chat/message/parts/DOCUMENTATION.md`
- present: `packages/ui/src/components/chat/message/parts/ToolPart.tsx`
- present: `packages/ui/src/components/chat/message/parts/taskToolModel.test.ts`
- present: `packages/ui/src/components/chat/message/parts/taskToolModel.ts`
- present: `packages/ui/src/components/chat/message/parts/toolPresentation.tsx`
- absent: `packages/ui/src/components/chat/message/unifiedExec.test.ts`
- absent: `packages/ui/src/components/chat/message/unifiedExec.ts`
- present: `packages/ui/src/hooks/useAssistantStatus.test.ts`
- present: `packages/ui/src/hooks/useAssistantStatus.ts`
- present: `packages/ui/src/lib/i18n/messages/de.ts`
- present: `packages/ui/src/lib/i18n/messages/en.ts`
- present: `packages/ui/src/lib/i18n/messages/es.ts`
- present: `packages/ui/src/lib/i18n/messages/fr.ts`
- present: `packages/ui/src/lib/i18n/messages/ja.ts`
- present: `packages/ui/src/lib/i18n/messages/ko.ts`
- present: `packages/ui/src/lib/i18n/messages/pl.ts`
- present: `packages/ui/src/lib/i18n/messages/pt-BR.ts`
- present: `packages/ui/src/lib/i18n/messages/uk.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-CN.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-TW.ts`
- present: `packages/ui/src/lib/toolHelpers.ts`

### 6dd45675f0 Local: feat(ui): refine activity and tool presentation

- present: `packages/ui/src/components/chat/MessageList.tsx`
- present: `packages/ui/src/components/chat/components/LiveTurnActivity.test.tsx`
- present: `packages/ui/src/components/chat/components/LiveTurnActivity.tsx`
- present: `packages/ui/src/components/chat/components/TurnActivity.tsx`
- present: `packages/ui/src/components/chat/hooks/useTurnRecords.ts`
- present: `packages/ui/src/components/chat/lib/turns/liveActivity.test.ts`
- present: `packages/ui/src/components/chat/lib/turns/liveActivitySummary.ts`
- present: `packages/ui/src/components/chat/lib/turns/projectTurnActivity.ts`
- present: `packages/ui/src/components/chat/lib/turns/projectTurnRecords.test.ts`
- present: `packages/ui/src/components/chat/lib/turns/projectTurnRecords.ts`
- present: `packages/ui/src/components/chat/lib/turns/streamingTailEntry.test.ts`
- present: `packages/ui/src/components/chat/lib/turns/streamingTailEntry.ts`
- present: `packages/ui/src/components/chat/lib/turns/turnProjectionCache.test.ts`
- present: `packages/ui/src/components/chat/lib/turns/turnProjectionCache.ts`
- present: `packages/ui/src/components/chat/lib/turns/types.ts`
- present: `packages/ui/src/components/chat/message/MessageBody.tsx`
- present: `packages/ui/src/components/chat/message/parts/DOCUMENTATION.md`
- present: `packages/ui/src/components/chat/message/parts/JustificationBlock.tsx`
- present: `packages/ui/src/components/chat/message/parts/ProgressiveGroup.tsx`
- present: `packages/ui/src/components/chat/message/parts/ToolPart.tsx`
- absent: `packages/ui/src/components/chat/message/parts/VirtualizedCodeBlock.test.ts`
- present: `packages/ui/src/components/chat/message/parts/toolRenderUtils.test.ts`
- present: `packages/ui/src/components/chat/message/parts/toolRenderUtils.ts`
- present: `packages/ui/src/components/chat/message/renderCompare.ts`
- present: `packages/ui/src/components/chat/message/toolRenderers.tsx`
- present: `packages/ui/src/lib/i18n/messages/de.ts`
- present: `packages/ui/src/lib/i18n/messages/en.ts`
- present: `packages/ui/src/lib/i18n/messages/es.ts`
- present: `packages/ui/src/lib/i18n/messages/fr.ts`
- present: `packages/ui/src/lib/i18n/messages/ja.ts`
- present: `packages/ui/src/lib/i18n/messages/ko.ts`
- present: `packages/ui/src/lib/i18n/messages/pl.ts`
- present: `packages/ui/src/lib/i18n/messages/pt-BR.ts`
- present: `packages/ui/src/lib/i18n/messages/tr.ts`
- present: `packages/ui/src/lib/i18n/messages/uk.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-CN.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-TW.ts`

### fbf230dcc0 Local: feat(ui): enforce authoritative session state

- present: `packages/ui/src/components/chat/AutoReviewBanner.tsx`
- present: `packages/ui/src/components/chat/ChatContainer.tsx`
- present: `packages/ui/src/components/chat/ChatInput.tsx`
- present: `packages/ui/src/components/chat/ChatMessage.tsx`
- present: `packages/ui/src/components/chat/TimelineDialog.tsx`
- present: `packages/ui/src/components/chat/composer/ui/RevertedMessageDock.tsx`
- present: `packages/ui/src/components/chat/message/MessageBody.tsx`
- present: `packages/ui/src/components/chat/message/parts/ToolPart.tsx`
- absent: `packages/ui/src/components/chat/message/unifiedExec.test.ts`
- absent: `packages/ui/src/components/chat/message/unifiedExec.ts`
- present: `packages/ui/src/components/chat/revertedMessageDockState.test.ts`
- present: `packages/ui/src/components/chat/revertedMessageDockState.ts`
- present: `packages/ui/src/hooks/useKeyboardShortcuts.ts`
- absent: `packages/ui/src/hooks/useSessionActivity.test.ts`
- present: `packages/ui/src/hooks/useSessionActivity.ts`
- present: `packages/ui/src/lib/multirun/fusion.ts`
- present: `packages/ui/src/lib/opencode/client.ts`
- present: `packages/ui/src/lib/sessionGoalActions.ts`
- present: `packages/ui/src/sync/DOCUMENTATION.md`
- absent: `packages/ui/src/sync/__tests__/event-pipeline.test.js`
- present: `packages/ui/src/sync/__tests__/event-reducer.test.js`
- absent: `packages/ui/src/sync/__tests__/event-reducer.test.ts`
- present: `packages/ui/src/sync/__tests__/eviction.test.ts`
- present: `packages/ui/src/sync/__tests__/materialization.test.ts`
- present: `packages/ui/src/sync/__tests__/session-status-snapshot.test.ts`
- present: `packages/ui/src/sync/bootstrap.test.ts`
- present: `packages/ui/src/sync/bootstrap.ts`
- present: `packages/ui/src/sync/child-store.test.ts`
- present: `packages/ui/src/sync/child-store.ts`
- present: `packages/ui/src/sync/event-pipeline.test.ts`
- present: `packages/ui/src/sync/event-pipeline.ts`
- present: `packages/ui/src/sync/event-reducer.ts`
- present: `packages/ui/src/sync/materialization.ts`
- absent: `packages/ui/src/sync/message-boundary.test.ts`
- absent: `packages/ui/src/sync/message-boundary.ts`
- absent: `packages/ui/src/sync/message-order.test.ts`
- absent: `packages/ui/src/sync/message-order.ts`
- present: `packages/ui/src/sync/message-ordering.test.ts`
- present: `packages/ui/src/sync/message-ordering.ts`
- present: `packages/ui/src/sync/optimistic.ts`
- present: `packages/ui/src/sync/reconnect-recovery.test.ts`
- present: `packages/ui/src/sync/reconnect-recovery.ts`
- present: `packages/ui/src/sync/session-actions.test.ts`
- present: `packages/ui/src/sync/session-actions.ts`
- present: `packages/ui/src/sync/session-cache.ts`
- present: `packages/ui/src/sync/session-message-loader.test.ts`
- present: `packages/ui/src/sync/session-message-loader.ts`
- present: `packages/ui/src/sync/session-message-records.test.ts`
- present: `packages/ui/src/sync/session-ui-store.ts`
- present: `packages/ui/src/sync/sync-context.tsx`
- present: `packages/ui/src/sync/types.ts`
- present: `packages/ui/src/sync/use-sync.test.ts`
- present: `packages/ui/src/sync/use-sync.ts`
- present: `packages/ui/src/sync/user-message-history.test.ts`
- present: `packages/ui/src/sync/user-message-history.ts`

### 0bca53a845 Local: fix(ui): respect disabled custom question answers

- absent: `packages/ui/src/components/chat/QuestionCard.test.tsx`
- absent: `packages/ui/src/components/chat/QuestionCard.tsx`
- absent: `packages/ui/src/types/question.ts`

### 015f379bad Local: fix(electron): stabilize desktop lifecycle

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- present: `packages/electron/preload.mjs`
- present: `packages/electron/scripts/electron-dev.mjs`
- present: `packages/electron/startup-url-selection.mjs`
- present: `packages/electron/startup-url-selection.test.mjs`
- present: `packages/web/server/index.d.ts`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/lifecycle.js`
- present: `packages/web/server/lib/opencode/lifecycle.test.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.test.js`
- present: `packages/web/vite.config.ts`
- present: `scripts/dev-web-hmr.mjs`

### b64db93a26 Local: fix(ui): persist shared settings and rename ownership

- present: `packages/ui/src/components/session/sidebar/sessions/SessionNodeItem.tsx`
- present: `packages/ui/src/lib/persistence.test.ts`
- present: `packages/web/server/lib/opencode/settings-helpers.js`
- present: `packages/web/server/lib/opencode/settings-helpers.test.js`

### 4c08fe9ac4 Local: feat(electron): bundle local OpenCode Candidate backend

- present: `packages/electron/README.md`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`

### ab2f496d60 Local: fix(sync): own SSE reconnect recovery

- present: `packages/ui/src/lib/opencode/client.ts`
- present: `packages/ui/src/sync/DOCUMENTATION.md`
- present: `packages/ui/src/sync/__tests__/event-pipeline-online.test.js`
- present: `packages/ui/src/sync/__tests__/event-pipeline-permanent-error.test.js`
- absent: `packages/ui/src/sync/__tests__/event-reducer.test.ts`
- present: `packages/ui/src/sync/__tests__/session-status-snapshot.test.ts`
- present: `packages/ui/src/sync/event-pipeline.test.ts`
- present: `packages/ui/src/sync/event-pipeline.ts`
- present: `packages/ui/src/sync/event-reducer.ts`
- present: `packages/ui/src/sync/live-aggregate-performance.test.ts`
- present: `packages/ui/src/sync/live-aggregate.ts`
- present: `packages/ui/src/sync/sync-context.tsx`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/session-runtime.js`
- present: `packages/web/server/lib/opencode/session-runtime.test.js`

### 70eaedb590 Local: feat(ui): show actionable provider errors

- present: `packages/ui/src/components/chat/ChatContainer.tsx`
- present: `packages/ui/src/components/chat/ChatMessage.tsx`
- present: `packages/ui/src/components/chat/MessageList.tsx`
- present: `packages/ui/src/components/chat/ModelControls.tsx`
- absent: `packages/ui/src/components/chat/lib/turns/applyRetryOverlay.test.ts`
- present: `packages/ui/src/components/chat/lib/turns/applyRetryOverlay.ts`
- present: `packages/ui/src/components/chat/message/MessageBody.tsx`
- present: `packages/ui/src/lib/i18n/messages/de.ts`
- present: `packages/ui/src/lib/i18n/messages/en.ts`
- present: `packages/ui/src/lib/i18n/messages/es.ts`
- present: `packages/ui/src/lib/i18n/messages/fr.ts`
- present: `packages/ui/src/lib/i18n/messages/ja.ts`
- present: `packages/ui/src/lib/i18n/messages/ko.ts`
- present: `packages/ui/src/lib/i18n/messages/pl.ts`
- present: `packages/ui/src/lib/i18n/messages/pt-BR.ts`
- present: `packages/ui/src/lib/i18n/messages/uk.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-CN.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-TW.ts`
- present: `packages/ui/src/sync/event-pipeline.test.ts`
- present: `packages/ui/src/sync/event-pipeline.ts`

### 4543a2c8c5 Local: fix(web): back off upstream event reconnects

- present: `packages/web/server/lib/event-stream/DOCUMENTATION.md`
- present: `packages/web/server/lib/event-stream/global-hub.js`
- present: `packages/web/server/lib/event-stream/global-hub.test.js`
- present: `packages/web/server/lib/event-stream/index.js`
- present: `packages/web/server/lib/event-stream/upstream-reader.js`
- present: `packages/web/server/lib/event-stream/upstream-reader.test.js`

### 1cf64090cd Local: fix(electron): target bundled OpenCode builds explicitly

- present: `packages/electron/README.md`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/target-architecture.mjs`
- present: `packages/electron/scripts/target-architecture.test.mjs`

### eb2c467ccd Local: test(ui): preserve sequence semantics in history fixtures

- present: `packages/ui/src/sync/session-message-loader.test.ts`

### 92c93f33e4 Local: coordinate desktop shutdown

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- absent: `packages/electron/quit-page.mjs`
- absent: `packages/electron/quit-page.test.mjs`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/verify-opencode-cli.mjs`
- present: `packages/electron/ssh-manager.mjs`
- present: `packages/electron/ssh-manager.test.mjs`
- present: `packages/vscode/src/opencode.ts`
- present: `packages/web/server/index.d.ts`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/cloudflare-tunnel.js`
- present: `packages/web/server/lib/dictation/DOCUMENTATION.md`
- present: `packages/web/server/lib/dictation/local/model-downloader.js`
- present: `packages/web/server/lib/dictation/service.js`
- absent: `packages/web/server/lib/dictation/service.test.js`
- present: `packages/web/server/lib/ngrok-tunnel.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/env-runtime.js`
- present: `packages/web/server/lib/opencode/env-runtime.test.js`
- present: `packages/web/server/lib/opencode/lifecycle.js`
- present: `packages/web/server/lib/opencode/lifecycle.test.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.test.js`
- present: `packages/web/server/lib/permission-auto-accept/DOCUMENTATION.md`
- present: `packages/web/server/lib/permission-auto-accept/runtime.js`
- present: `packages/web/server/lib/permission-auto-accept/runtime.test.js`
- present: `packages/web/server/lib/realtime-proxy.js`
- present: `packages/web/server/lib/relay/DOCUMENTATION.md`
- present: `packages/web/server/lib/relay/service.js`
- present: `packages/web/server/lib/relay/service.test.js`
- present: `packages/web/server/lib/scheduled-tasks/DOCUMENTATION.md`
- present: `packages/web/server/lib/scheduled-tasks/runtime.js`
- present: `packages/web/server/lib/scheduled-tasks/runtime.test.js`
- present: `packages/web/server/lib/scheduled-tasks/service.js`
- present: `packages/web/server/lib/terminal/runtime.js`
- present: `packages/web/server/lib/terminal/runtime.test.js`

### db30c394cb Local: disable NSIS desktop shortcut

- present: `packages/electron/package.json`

### 5086f60e7f Local: harden OpenChamber agent tool contract

- present: `packages/web/server/lib/agent-tool/DOCUMENTATION.md`
- present: `packages/web/server/lib/agent-tool/runtime.js`
- present: `packages/web/server/lib/agent-tool/runtime.test.js`
- present: `packages/web/server/lib/openchamber-control/actions.js`

### 07ee9d7bfb Local: Confirm before quitting active conversations

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- present: `packages/web/server/index.d.ts`
- present: `packages/web/server/index.js`

### 1599badfe8 Local: adapt OpenChamber to unified exec lanes

- present: `packages/ui/src/components/chat/lib/turns/projectTurnRecords.test.ts`
- present: `packages/ui/src/components/chat/message/parts/DOCUMENTATION.md`
- present: `packages/ui/src/components/chat/message/parts/ToolPart.tsx`
- present: `packages/ui/src/components/chat/message/parts/taskToolModel.test.ts`
- present: `packages/ui/src/components/chat/message/parts/toolPresentation.tsx`
- absent: `packages/ui/src/components/chat/message/unifiedExec.test.ts`
- absent: `packages/ui/src/components/chat/message/unifiedExec.ts`
- present: `packages/ui/src/hooks/useAssistantStatus.test.ts`
- present: `packages/ui/src/hooks/useAssistantStatus.ts`
- present: `packages/ui/src/lib/i18n/messages/de.ts`
- present: `packages/ui/src/lib/i18n/messages/en.ts`
- present: `packages/ui/src/lib/i18n/messages/es.ts`
- present: `packages/ui/src/lib/i18n/messages/fr.ts`
- present: `packages/ui/src/lib/i18n/messages/ja.ts`
- present: `packages/ui/src/lib/i18n/messages/ko.ts`
- present: `packages/ui/src/lib/i18n/messages/pl.ts`
- present: `packages/ui/src/lib/i18n/messages/pt-BR.ts`
- present: `packages/ui/src/lib/i18n/messages/uk.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-CN.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-TW.ts`
- present: `packages/ui/src/lib/sessionEvents.ts`

### 7e7ca95f93 Local: bound revert history loading

- present: `packages/ui/src/components/chat/ChatContainer.tsx`
- present: `packages/ui/src/sync/DOCUMENTATION.md`
- present: `packages/ui/src/sync/__tests__/issue-2039.test.ts`
- present: `packages/ui/src/sync/session-actions.test.ts`
- present: `packages/ui/src/sync/session-actions.ts`
- present: `packages/ui/src/sync/session-message-loader.test.ts`
- present: `packages/ui/src/sync/session-message-loader.ts`
- present: `packages/ui/src/sync/session-ui-store.ts`
- present: `packages/ui/src/sync/use-sync.ts`

### 3f53359eb7 Local: preserve question answers in session messages

- present: `packages/web/server/lib/agent-tool/runtime.test.js`
- present: `packages/web/server/lib/openchamber-control/DOCUMENTATION.md`
- present: `packages/web/server/lib/openchamber-control/actions.js`
- present: `packages/web/server/lib/openchamber-control/service.js`
- present: `packages/web/server/lib/openchamber-control/service.test.js`

### 52eb6c159b Local: adapt agent tool contract to OpenChamber v1.19.0

- present: `packages/web/server/lib/agent-tool/DOCUMENTATION.md`
- present: `packages/web/server/lib/agent-tool/runtime.js`
- present: `packages/web/server/lib/agent-tool/runtime.test.js`

### cd6fa01ab2 Local: Point desktop updater at fork releases

- present: `packages/electron/package.json`
- present: `packages/electron/updater-feed.mjs`
- present: `packages/electron/updater-feed.test.mjs`

### 4119273bf4 Local: Enable memory visibility by default

- present: `packages/ui/src/components/sections/openchamber/OpenChamberToolsSettings.tsx`
- present: `packages/ui/src/stores/useUIStore.ts`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/agent-memory/feature-flag.js`
- present: `packages/web/server/lib/agent-memory/feature-flag.test.js`
- present: `packages/web/server/lib/session-knowledge/DOCUMENTATION.md`

### 7e0da64fa8 Local: build(electron): package Recycle Bin helper

- present: `packages/electron/README.md`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/verify-opencode-cli.mjs`

### 23df0d0cbc Local: use patched Bun for bundled CLI

- present: `packages/electron/README.md`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/target-architecture.mjs`
- present: `packages/electron/scripts/target-architecture.test.mjs`

### f0db18ef3c Local: Move latency-sensitive Git reads off main thread

- present: `packages/electron/README.md`
- present: `packages/ui/src/components/chat/ChatInput.tsx`
- present: `packages/ui/src/components/chat/work-status/WorkStatusPrimaryGroup.tsx`
- present: `packages/ui/src/components/mini-chat/MiniChatLayout.tsx`
- present: `packages/ui/src/components/multirun/BranchSelector.tsx`
- present: `packages/ui/src/components/session/sidebar/projects/useProjectRepoStatus.ts`
- present: `packages/ui/src/components/ui/CommandPalette.tsx`
- present: `packages/ui/src/components/views/DiffView.tsx`
- present: `packages/ui/src/hooks/useNestedGitDirectory.ts`
- present: `packages/ui/src/lib/api/types.ts`
- present: `packages/ui/src/lib/gitApiHttp.test.ts`
- present: `packages/ui/src/lib/gitApiHttp.ts`
- present: `packages/ui/src/stores/DOCUMENTATION.md`
- present: `packages/ui/src/stores/useGitStore.test.ts`
- present: `packages/ui/src/stores/useGitStore.ts`
- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/git-binary.js`
- absent: `packages/web/server/lib/git/git-binary.test.js`
- absent: `packages/web/server/lib/git/git-read-shared.js`
- absent: `packages/web/server/lib/git/git-read-shared.test.js`
- absent: `packages/web/server/lib/git/git-read-worker-client.js`
- absent: `packages/web/server/lib/git/git-read-worker-client.test.js`
- absent: `packages/web/server/lib/git/git-read-worker.js`
- present: `packages/web/server/lib/git/routes.js`
- present: `packages/web/server/lib/git/routes.test.js`
- present: `packages/web/server/lib/git/service.js`
- present: `packages/web/server/lib/git/service.test.js`
- absent: `packages/web/server/lib/git/status-tasks.js`
- present: `packages/web/server/lib/github/DOCUMENTATION.md`
- present: `packages/web/server/lib/github/pr-status.js`
- present: `packages/web/server/lib/github/pr-status.test.js`
- present: `packages/web/server/lib/github/routes.js`
- present: `packages/web/server/lib/opencode/env-runtime.js`
- present: `packages/web/src/api/git.ts`

### a6dcdfe017 Local: wait for managed startup migrations

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- absent: `packages/electron/quit-page.mjs`
- absent: `packages/electron/quit-page.test.mjs`
- present: `packages/web/server/index.d.ts`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/lifecycle.js`
- present: `packages/web/server/lib/opencode/lifecycle.test.js`
- present: `packages/web/server/lib/opencode/startup-pipeline-runtime.js`
- present: `packages/web/server/lib/opencode/startup-pipeline-runtime.test.js`

### 605e0f0a9b Local: Skip redundant desktop port persistence

- present: `packages/electron/main.mjs`

### fa0d0e1635 Local: bundle GUI subsystem OpenCode

- present: `packages/electron/README.md`
- present: `packages/electron/package.json`
- absent: `packages/electron/scripts/pe-subsystem.mjs`
- absent: `packages/electron/scripts/pe-subsystem.test.mjs`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/verify-opencode-cli.mjs`

### e394cdf873 Local: run OpenCode build with selected Bun

- present: `packages/electron/scripts/prepare-opencode-cli.mjs`

### 600b90c092 Local: route Windows Git through process broker

- absent: `bun-patches/simple-git@3.36.0.patch`
- present: `bun.lock`
- present: `package.json`
- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- present: `packages/electron/package.json`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/verify-opencode-cli.mjs`
- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/git-read-worker-client.js`
- absent: `packages/web/server/lib/git/process-broker.js`
- absent: `packages/web/server/lib/git/process-broker.test.js`
- present: `packages/web/server/lib/git/service.js`
- absent: `packages/web/server/lib/git/simple-git-spawn.test.js`

### 5ad7f5eb84 Local: adapt message materialization to OpenChamber v1.20.0

- absent: `packages/ui/src/components/chat/message/parts/VirtualizedCodeBlock.test.ts`
- absent: `packages/ui/src/sync/__tests__/event-reducer.test.ts`
- present: `packages/ui/src/sync/materialization.ts`

### c4e827612e Local: support explicit Bun packaging runtime

- present: `packages/electron/scripts/package.mjs`
- present: `packages/electron/scripts/prepare-opencode-cli.mjs`
- present: `packages/electron/scripts/target-architecture.mjs`
- present: `packages/electron/scripts/target-architecture.test.mjs`

### ec9214cd80 Local: make Electron own Windows env probing

- present: `packages/electron/main.mjs`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/env-runtime.js`
- present: `packages/web/server/lib/opencode/env-runtime.test.js`

### ecb6923185 Local: overlap Windows env probing with startup

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- absent: `packages/electron/startup-single-flight.mjs`
- absent: `packages/electron/startup-single-flight.test.mjs`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/env-runtime.js`
- present: `packages/web/server/lib/opencode/env-runtime.test.js`

### 6a9feb7e25 Local: move Windows env probing to worker thread

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/env-runtime.js`
- present: `packages/web/server/lib/opencode/env-runtime.test.js`
- absent: `packages/web/server/lib/opencode/windows-shell-env-worker.js`

### cbb886d93f Local: Lazy-load optional server dependencies

- present: `packages/web/server/lib/tts/routes.test.js`
- present: `packages/web/server/lib/tts/stt.js`
- present: `packages/web/server/lib/ui-auth/ui-auth.test.js`
- present: `packages/web/server/lib/ui-auth/ui-passkeys.js`

### 9a1d121954 Local: add server preload environment guard

- present: `packages/electron/README.md`
- absent: `packages/electron/fixtures/server-preload-env-reader.mjs`
- present: `packages/electron/package.json`
- absent: `packages/electron/server-preload-env.test.mjs`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- absent: `packages/web/server/preload-env-guard.js`

### 0474abeb46 Local: preload server dependencies during shell discovery

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- absent: `packages/electron/server-dependency-preload.mjs`
- absent: `packages/electron/server-dependency-preload.test.mjs`
- absent: `packages/electron/server-preload-env.test.mjs`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- absent: `packages/web/server/preload-server-dependencies.js`

### a17cee8782 Local: adapt migrated tests to Bun 1.4

- absent: `packages/web/server/lib/git/git-read-worker-client.test.js`
- present: `packages/web/server/lib/git/routes.test.js`
- present: `packages/web/server/lib/opencode/lifecycle.test.js`

### 50695d3771 Local: use native promises in Bun git tests

- absent: `packages/web/server/lib/git/simple-git-spawn.test.js`

### 1799d3dc05 Local: adapt Git status migration to v1.22.1

- present: `packages/ui/src/components/session/ForkSessionDialog.tsx`
- present: `packages/ui/src/stores/useGitStore.ts`
- present: `packages/ui/src/sync/__tests__/session-switch-resync.test.ts`

### 4710256ff0 Local: fix migrated UI lint diagnostics

- present: `packages/ui/src/components/chat/message/parts/ToolPart.tsx`

### 55ceff45ec Local: preserve sequence state across directory moves

- present: `packages/ui/src/sync/DOCUMENTATION.md`
- present: `packages/ui/src/sync/session-actions.test.ts`
- present: `packages/ui/src/sync/session-actions.ts`
- present: `packages/ui/src/sync/session-message-loader.test.ts`
- present: `packages/ui/src/sync/session-message-loader.ts`

### 475018b685 Local: use sequence order for Goal truncation recovery

- present: `packages/web/server/lib/session-goal/DOCUMENTATION.md`
- present: `packages/web/server/lib/session-goal/runtime.js`
- present: `packages/web/server/lib/session-goal/runtime.test.js`

### d64527e6a7 Local: test delayed terminal spawn during shutdown

- present: `packages/web/server/lib/terminal/runtime.test.js`

### 65d1e13526 Local: Limit SDK preload to client entry for Linux builds

- absent: `packages/web/server/preload-server-dependencies.js`

### cd2e8ad37d Local: Prevent user message actions from widening bubbles

- present: `packages/ui/src/components/chat/message/MessageBody.tsx`

### c0bec36246 Local: Start desktop UI alongside OpenCode with migration overlay

- present: `packages/electron/README.md`
- absent: `packages/electron/desktop-startup.mjs`
- absent: `packages/electron/desktop-startup.test.mjs`
- present: `packages/electron/main.mjs`
- present: `packages/electron/preload.mjs`
- absent: `packages/electron/quit-page.mjs`
- absent: `packages/electron/quit-page.test.mjs`
- present: `packages/ui/src/App.tsx`
- present: `packages/ui/src/apps/ElectronMiniChatApp.tsx`
- present: `packages/ui/src/apps/renderElectronMiniChatApp.tsx`
- present: `packages/ui/src/apps/renderMobileApp.tsx`
- present: `packages/ui/src/components/layout/ContextPanel.tsx`
- present: `packages/ui/src/components/layout/__tests__/issue-2815-sessionChatIframesMountAllTabs.test.ts`
- present: `packages/ui/src/index.css`
- absent: `packages/ui/src/lib/desktop-startup.test.ts`
- absent: `packages/ui/src/lib/desktop-startup.ts`
- present: `packages/ui/src/lib/i18n/messages/de.ts`
- present: `packages/ui/src/lib/i18n/messages/en.ts`
- present: `packages/ui/src/lib/i18n/messages/es.ts`
- present: `packages/ui/src/lib/i18n/messages/fr.ts`
- present: `packages/ui/src/lib/i18n/messages/ja.ts`
- present: `packages/ui/src/lib/i18n/messages/ko.ts`
- present: `packages/ui/src/lib/i18n/messages/pl.ts`
- present: `packages/ui/src/lib/i18n/messages/pt-BR.ts`
- present: `packages/ui/src/lib/i18n/messages/tr.ts`
- present: `packages/ui/src/lib/i18n/messages/uk.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-CN.ts`
- present: `packages/ui/src/lib/i18n/messages/zh-TW.ts`
- present: `packages/ui/src/lib/opencode/client.ts`
- present: `packages/ui/src/main.tsx`
- present: `packages/web/index.html`
- present: `packages/web/mini-chat.html`
- present: `packages/web/server/index.d.ts`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/lifecycle.js`
- present: `packages/web/server/lib/opencode/lifecycle.test.js`
- present: `packages/web/server/lib/opencode/proxy.js`
- present: `packages/web/server/opencode-proxy.test.js`
- present: `packages/web/src/main.tsx`
- present: `packages/web/src/mini-chat-main.tsx`

### a3fb2c3741 Local: fix(git): keep workers referenced only during active requests

- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/git-read-worker-client.js`
- absent: `packages/web/server/lib/git/git-read-worker-client.test.js`

### c0f9bd259f Local: Honor the default Windows file manager for directory actions

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- absent: `packages/electron/native/windows-shell/binding.gyp`
- absent: `packages/electron/native/windows-shell/windows-shell.cpp`
- present: `packages/electron/package.json`
- present: `packages/electron/scripts/after-pack.cjs`
- absent: `packages/electron/scripts/build-windows-shell.mjs`
- present: `packages/electron/scripts/rebuild-native.mjs`
- absent: `packages/electron/windows-shell.mjs`
- absent: `packages/electron/windows-shell.test.mjs`

### ec8ff3b99e Local: adapt v1.23.1 UI fixtures and translations

- present: `packages/ui/src/lib/i18n/messages/tr.ts`
- present: `packages/ui/src/lib/sessionTitle.test.ts`

### 7024985d70 Local: confirm missing sessions before deletion cleanup

- present: `packages/ui/src/sync/DOCUMENTATION.md`
- present: `packages/ui/src/sync/session-actions.ts`
- present: `packages/ui/src/sync/session-retention.test.ts`

### c12c28a7e9 Local: wait for broker close after output overflow

- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/process-broker.js`
- absent: `packages/web/server/lib/git/process-broker.test.js`

### f75a7bdeb9 Local: test(git): cover late broker cancellation isolation

- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/process-broker.test.js`

### 69c5ea7201 Local: fix(sync): adapt recovery to v1.23.2

- present: `packages/ui/src/lib/opencode/client.status.test.ts`
- present: `packages/ui/src/lib/opencode/session-status.ts`
- present: `packages/ui/src/sync/DOCUMENTATION.md`
- present: `packages/ui/src/sync/__tests__/session-status-snapshot.test.ts`
- present: `packages/ui/src/sync/bootstrap.ts`
- present: `packages/ui/src/sync/directory-recovery-snapshots.test.ts`
- present: `packages/ui/src/sync/directory-recovery-snapshots.ts`
- present: `packages/ui/src/sync/sync-context.tsx`

### 8c200d0e52 Local: adapt fork integration to OpenChamber v1.24.0

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- present: `packages/electron/server-shutdown.mjs`
- present: `packages/electron/server-shutdown.test.mjs`
- present: `packages/ui/src/components/chat/message/parts/DOCUMENTATION.md`
- present: `packages/ui/src/components/chat/message/parts/ToolPart.guest.test.tsx`
- present: `packages/ui/src/components/chat/message/parts/ToolPart.tsx`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/guests/DOCUMENTATION.md`
- absent: `packages/web/server/lib/guests/service-shutdown.test.js`
- present: `packages/web/server/lib/guests/service.js`
- present: `packages/web/server/lib/guests/service.test.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/lifecycle.js`
- present: `packages/web/server/lib/opencode/lifecycle.test.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.test.js`
- present: `packages/web/server/lib/terminal/runtime.test.js`

### cd9fb0bb4b Local: fix(git): isolate broker errors and await worktree test cleanup

- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/process-broker.js`
- absent: `packages/web/server/lib/git/process-broker.test.js`
- present: `packages/web/server/lib/git/service.test.js`

### 1b93eda3e9 Local: finish Git status scheduler integration

- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- present: `packages/web/server/lib/git/service.js`
- present: `packages/web/server/lib/git/service.test.js`

### 4a7709ea17 Local: fix session rename menu close handoff

- absent: `packages/ui/src/components/session/sidebar/sessions/SessionNodeItem.rename.vitest.tsx`

### 72ccaef77f Local: reconcile host-guided recovery through scoped atomic commits

- present: `packages/ui/src/hooks/useGlobalSessionsPolling.ts`
- present: `packages/ui/src/lib/opencode/session-status.ts`
- present: `packages/ui/src/sync/DOCUMENTATION.md`
- present: `packages/ui/src/sync/__tests__/session-switch-resync.test.ts`
- present: `packages/ui/src/sync/bootstrap.ts`
- present: `packages/ui/src/sync/directory-recovery-snapshots.test.ts`
- present: `packages/ui/src/sync/directory-recovery-snapshots.ts`
- present: `packages/ui/src/sync/global-blocking-requests.test.ts`
- present: `packages/ui/src/sync/global-blocking-requests.ts`
- present: `packages/ui/src/sync/host-session-status-seed.test.ts`
- present: `packages/ui/src/sync/host-session-status-seed.ts`
- present: `packages/ui/src/sync/sync-context.tsx`

### 149a63ab90 Local: coordinate terminal grace and installer-owned desktop shutdown

- present: `packages/electron/README.md`
- present: `packages/electron/main.mjs`
- absent: `packages/electron/quit-page.mjs`
- absent: `packages/electron/updater-install.mjs`
- absent: `packages/electron/updater-install.test.mjs`
- present: `packages/web/server/index.js`
- present: `packages/web/server/lib/opencode/DOCUMENTATION.md`
- present: `packages/web/server/lib/opencode/shutdown-runtime.js`
- present: `packages/web/server/lib/opencode/shutdown-runtime.test.js`

### 9860926679 Local: preserve broker ownership across Git status timeouts

- present: `packages/web/server/lib/git/DOCUMENTATION.md`
- absent: `packages/web/server/lib/git/process-broker.js`
- absent: `packages/web/server/lib/git/process-broker.test.js`
- present: `packages/web/server/lib/git/service.js`

### 723d124707 Local: verify sequence-aware fusion after upstream module migration

- present: `packages/ui/src/lib/multirun/DOCUMENTATION.md`
- present: `packages/ui/src/lib/multirun/fusion.test.ts`

## Complete upstream commit inventory

Area labels identify paths only; they do not assert behavior or review completion.

- 1e650924cb [packages/ui/src/components] fix(chat): skip external favicons in image exports
- bd86f4a897 [packages/ui/src/components, packages/ui/src/lib] fix(chat): 移动端仅允许通过按钮发送消息
- 18d7cf8cd0 [] Merge upstream main into fix/mobile-send
- ec11d02b55 [packages/ui/src/apps] fix(mobile): prevent text selection from triggering edge swipes
- dca0239a38 [packages/ui/src/components, packages/ui/src/styles] feat(ui): replace mobile comment overlay with composer comment mode
- 5883d28aff [.github, bun.lock, packages/docs, packages/sdk, packages/ui/src/App.tsx, packages/ui/src/components, packages/ui/src/hooks, packages/ui/src/lib, packages/ui/src/stores, packages/vscode, packages/web/server/index.js, packages/web/server/lib] feat(sdk): browser provider role and shared surface for extension services (#3734)
- 601270072a [] fix(chat): merge export favicon fix (#3451)
- e01b0660f4 [] fix(mobile): merge selection swipe isolation (#3718)
- 90d2e9f9c3 [] fix(chat): merge mobile newline policy (#3709)
- 499700ab19 [] feat(chat): merge mobile comment composer (#3733)
- 71d68cd130 [packages/ui/src/components, packages/ui/src/lib] fix(chat): preserve mobile shortcuts and comment drafts after integration
- f6318ae2cc [packages/ui/src/index.css] style: simplify chat comment highlight color
- 56f33fd59a [packages/ui/src/components] fix(chat): fill available width with comment quote preview
- de67c93913 [packages/ui/src/components] fix(chat): preserve explicit steer after dismissing blockers (#3369)
- 49a9aff7e5 [packages/ui/src/components] fix(desktop): preserve SSH form input focus after confirmations
- 6c980f29f5 [packages/ui/src/components, packages/ui/src/lib] fix(desktop): report pairing import failures accurately
- 2832c9254a [packages/ui/src/App.tsx, packages/ui/src/lib, packages/web/bin/lib] fix(startup): show OpenCode failure diagnostics during recovery
- d8dbd2567c [packages/ui/src/index.css] fix(chat): restore translucent comment highlighting
- be8eed934d [packages/ui/src/components, packages/web/server/lib] fix(chat): preview permission file patches
- 66b42b32c2 [docs, packages/web/server/lib] feat(spaces): unwired Docker place and space manager, with the isolated spaces design (#3749)
- d63d1bf9cf [packages/ui/src/components, packages/ui/src/sync] docs(chat): clarify steer delivery after blocker dismissal
- 959d179c6a [packages/docs, packages/sdk, packages/ui/src/components, packages/ui/src/lib, packages/web/server/lib] feat(sdk): scope browser provider calls and dock a page beside a shared surface
- 8f02f56fb5 [packages/ui/src/components] fix(chat): stop the pinned section retaining every transcript's parts
- 449d9b42ac [packages/ui/src/components, packages/ui/src/hooks, packages/ui/src/lib, packages/ui/src/sync, scripts] feat(sync): evict idle session histories whole instead of trimming turns
- e601f16271 [knip.json, package.json, packages/electron, packages/web, packages/web/server/index.js, packages/web/server/lib, scripts] perf(desktop): show the window before loading the main process
- 83fefdf223 [packages/web/server/index.js, packages/web/server/lib] perf(server): load the OpenAI, WebAuthn, jose and web-push modules on first use
- 918eb93000 [docs, packages/web/server/lib] feat(spaces): tools volume and the server inside a space (#3771)
- a91777602b [packages/ui/src/components, packages/ui/src/lib, packages/web/server/lib, packages/web/src/api] fix(files): open outside-workspace files without grants
- e1ed8727b3 [packages/electron] fix(desktop): run the bundled OpenCode CLI in dev and clear stale HMR chunks
- 40a17b11ad [packages/ui/src/stores, packages/ui/src/sync] perf(sessions): paint the first page of the global session list immediately
- 7ad4b28974 [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/stores, packages/vscode, packages/web/server/lib] feat(sidebar): add the Timeline view and a Grouped/Timeline switch
- fbf027ada3 [packages/ui/src/components, scripts] perf(sidebar): memoize the session row wrapper
- 896776d81e [packages/ui/src/components, packages/ui/src/lib, packages/ui/src/sync] fix(sessions): route actions to the session's own directory and explain failures
- 0ca21cf8df [packages/ui/src/components] fix: clear timeline reveal fade to keep overlay scrollbar interactive
- deb25dc0d5 [packages/ui/src/apps, packages/ui/src/stores] fix(mobile): keep branch lines and row order through the sessions drawer exit
- 1c2bd65615 [packages/web/server/lib] fix(github): attribute a merged PR to a branch only when its commits are checked out
- 9480e0fab4 [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib] fix(mobile): make settings hints tappable and smooth four phone rough edges
- 8c9e08234d [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/stores, packages/web/server/index.js, packages/web/server/lib] feat(files): rich artifact previews and an agent file.open action
- 00ee267dc8 [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/stores, packages/ui/src/sync] fix(sessions): keep restored worktree sessions visible (#3545)
- 83ec4fbde2 [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/index.css, packages/ui/src/lib, packages/ui/src/stores, scripts] feat(ui): add opt-in session activity spinners (#3459)
- 8e51f9ad65 [] Merge remote-tracking branch 'origin/main' into bohdan/dev
- 45ff8c6484 [docs, packages/web/server/lib] feat(spaces): a gatekeeper as a space's only way out (#3790)
- 7a21ba6a56 [packages/ui/src/sync] test(sync): keep count-limit retention tests off the idle-grace clock (#3795)
- 1eb8d5c231 [.create-pr-proof, packages/ui/src/components, packages/ui/src/lib] fix(settings): reuse the chat project picker drawer for mobile project selection (#2999) (#3024)
- c469a40bf0 [packages/ui/src/lib] feat(review): inherit permission auto-accept in new review sessions
- 945abf48be [.agents, .claude, AGENTS.md, docs] docs(spaces): add a skill for changes to an isolated space's boundary (#3798)
- 03fe441cef [.create-pr-proof, packages/ui/src/components] fix(ui): keep failed dictation recovery controls on screen (#3026)
- 02cdb8c957 [packages/ui/src/hooks, packages/ui/src/lib, packages/web/server/index.js, packages/web/server/lib] fix(notifications): share the control event stream across web consumers
- 0d68efeeb4 [packages/web/server/lib] fix(small-model): resolve Codex Luna from catalog
- 1938b9c5fb [packages/ui/src/components, packages/ui/src/hooks, packages/ui/src/lib, packages/web/server/lib] feat(diff): expand collapsed context in pull request comparisons
- 0cfb158e18 [.github, packages/ui/src/lib] chore(deps): update actions/create-github-app-token action to v3 (#1909)
- 13171ba218 [bun.lock, package.json, packages/web] fix(deps): update dependency node-pty to v1.2.0-beta.15 (#1601)
- a86c4eed60 [.github] chore(deps): update actions/checkout action to v7 (#1908)
- fe2ee07ed8 [packages/ui/src/components] fix(model-picker): scroll to and expand the selected model on open in Settings (#1915)
- 19ac676749 [bun.lock, packages/web] fix(deps): update dependency @simplewebauthn/server to v13.3.3 (#1907)
- e4f64319dd [] Merge remote-tracking branch 'origin/main' into bohdan/dev
- b939370c1c [changelog] docs(changelog): update unreleased notes and contributor credits
- fafbd50bc4 [.agents, packages/ui/src/components, packages/ui/src/index.css, packages/ui/src/lib] feat(settings): searchable theme picker, tighter dropdowns, stable scroll gutter
- 1dd4347537 [packages/ui/src/components] revert(model-picker): drop scroll-to-selected on open in Settings
- 93d7c9b753 [.agents, .github, .opencode, AGENTS.md, CONTRIBUTING.md, README.md, packages/electron] chore(contributing): route features through Ideas discussions and park large PRs
- 336e19248f [.agents] docs(triage-prs): record the needs-discussion cutover date
- 2426ede0e4 [.github, packages/ui/src/components] fix(sidebar): show the PR row in Recent worktree tooltips and share the worktree index (#3807)
- 8e75a1dc0c [packages/web/server/index.js, packages/web/server/lib] fix(server): stop guest services during graceful shutdown (#3812)
- ff8679be06 [packages/web/server/index.js, packages/web/server/lib] fix(server): fence guest starts during shared shutdown (#3829)
- 7be5f80bb2 [packages/ui/src/components, pr-evidence] fix(sidebar): resolve Recent subtask worktree metadata (#3830)
- fdd3a58a57 [.github, CONTRIBUTING.md] docs(discussions): ask ideas to start from the problem
- d8003eaaab [.agents, .github, CONTRIBUTING.md] docs(discussions): state that ideas are discussed before the code
- 2e98bb5671 [.github, CONTRIBUTING.md] docs(contributing): put the discussion-first rule in the PR instructions
- 43a469e2a2 [.agents, .github, CONTRIBUTING.md] docs(contributing): say that a product-decision PR without a go-ahead is not reviewed
- 0e72889ce2 [AGENTS.md, CONTRIBUTING.md] docs(contributing): define what counts as a product decision
- 0f79a82a92 [AGENTS.md] docs(agents): scope the discussion requirement to outside contributions
- 49f5c852d6 [.github] fix(pr-intake): exempt maintainers whose org membership is private
- f25d6f5036 [docs, packages/web/server/lib] feat(spaces): bring a project's code into a space (stage 3a) (#3833)
- 654705f7d8 [.agents, .claude, .github, AGENTS.md, Dockerfile, bun.lock, package.json, packages/electron, packages/sdk, packages/ui, packages/ui/src/App.tsx, packages/ui/src/apps, packages/ui/src/components, packages/ui/src/hooks, packages/ui/src/index.css, packages/ui/src/lib, packages/ui/src/stores, packages/ui/src/styles, packages/ui/src/sync, packages/ui/src/types, packages/vscode, packages/web, packages/web/server/index.js, packages/web/server/lib, packages/web/server/opencode-proxy.test.js, packages/web/server/opencode-worktree-readiness.test.js, packages/web/src/api, packages/web/test/bun-test-shim.ts, scripts, vite.config.ts] feat: move OpenChamber to OpenCode 2.x (#3837)
- 0c4fbe362d [packages/ui/src/components, packages/ui/src/lib] feat: add start session action to empty sidebar groups
- e91ab06beb [packages/ui/src/components] fix(chat): restore v2 edit and patch diff counts
- 90a8d8f109 [packages/ui/src/components, packages/ui/src/hooks, packages/ui/src/stores, packages/ui/src/sync] fix(sessions): recover sidebar lists after startup failures
- dad8588a4c [packages/web/server/index.js, packages/web/server/lib] fix(server): close upgraded sockets during graceful shutdown
- e80f253674 [packages/ui/src/App.tsx, packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/sync, packages/vscode, packages/web/server/index.js, packages/web/server/lib] fix(opencode): support CLI upgrades and v1 migration recovery
- 39a4940db9 [packages/ui/src/lib] feat(theme): add Osaka Jade Refined built-in palettes
- 431972e0f5 [packages/ui/src/lib] feat(theme): add built-in Cursor palettes
- 04282b084e [packages/ui/src/components, packages/ui/src/lib] feat(chat): describe permission requests in plain words
- a585e81c62 [packages/ui/src/lib, packages/web/server/lib] fix(sessions): wait for the v2 turn end, not a step end
- d7a0df9c71 [packages/ui/src/components, packages/ui/src/lib, packages/ui/src/sync] feat(chat): fork a session from an agent answer
- 5702507376 [packages/web/server/lib] fix(agent-tool): let explicit user requests override tool restraints
- f99fe1fd66 [packages/ui/src/lib, packages/ui/src/stores, packages/web/server/lib] feat(memory): tell every session when to save and deliver it everywhere
- 133577aebd [packages/ui/src/components] fix(agents): keep the resolved mode when an override omits it
- af9d928a9f [packages/ui/src/sync] fix(sessions): keep the whole record when archiving or restoring
- 662b81abac [packages/ui/src/sync] fix(sidebar): order sessions by their last turn, not any update
- 461f914521 [.agents, Dockerfile, bun.lock, package.json, packages/electron, packages/ui, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/sync, packages/vscode, packages/web, packages/web/server/index.js, packages/web/server/lib] feat(opencode): move session metadata onto OpenCode 2.0.15
- 9d4def8149 [packages/electron, packages/ui/src/components, packages/ui/src/index.css, packages/ui/src/lib, packages/ui/src/sync, packages/web/server/index.d.ts, packages/web/server/index.js, packages/web/server/lib] fix(desktop): improve startup and instance recovery
- 31c1be6c02 [packages/ui/src/components] fix(markdown): skip repeated parse after streaming lexer failure
- 2d406f9f9e [packages/ui/src/components, packages/ui/src/lib] feat(files): upload files from the folder menu and tree toolbar
- 6c1735a70f [packages/ui/src/sync] fix(sync): keep live subagent link when a stale snapshot lands
- 800b2aee9a [packages/ui/src/components] feat(providers): sign in to OpenCode Go through the Console
- 4e17f1b254 [packages/vscode] fix(vscode): resolve managed CLI consistently for upgrades
- 52230df845 [packages/ui/src/components] fix(ui): keep agent browser activity in the background
- f1058bcde0 [packages/web/server/lib] fix(server): end turns only when background subagents finish
- b88b95cfeb [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/hooks, packages/ui/src/lib, packages/ui/src/sync] fix(ui): keep a session's turn open while its subagents run
- 3793538edc [packages/ui/src/lib] fix(theme): soften built-in text and borders
- 0af1eb00ca [packages/web/server/lib] fix(memory): read each memory entry once per conversation
- 8b101d55d7 [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib, packages/vscode, packages/web/server/lib, scripts] fix: OpenCode v2 install on Windows and in VS Code, oc-dev on Windows, test runner (#3862)
- 10a7bd75f8 [packages/web/bin/lib, packages/web/server/index.js, packages/web/server/lib] fix(web): require UI password before starting public tunnels
- e33735799a [packages/ui/src/lib, packages/ui/src/sync] fix(ui): refresh Git state after live tool transitions
- 0a6198389b [.agents, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/stores] feat(plugins): show OpenCode load status and update unpinned plugins
- 142357ee73 [packages/ui/src/components, packages/ui/src/lib, packages/ui/src/sync] feat(chat): attach inline /skill mentions to the prompt
- 4e3b82c822 [packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/stores] feat(stats): add a usage stats page from OpenCode's session stats
- 69f2a32522 [.agents, packages/ui/src/apps, packages/ui/src/components, packages/ui/src/lib, packages/ui/src/stores, packages/ui/src/sync, packages/vscode, packages/web/server/lib] feat(websearch): show results as cards and add web search settings
- 9aeb6fe930 [packages/docs, packages/ui/src/components, packages/ui/src/lib, packages/web/server/lib] feat(providers): configure reasoning levels for custom provider models
- f45c1bff2e [packages/ui/src/components, packages/ui/src/lib] fix(stats): give model names room on phones
- 19112c58ed [CHANGELOG.md, bun.lock, changelog, package.json, packages/electron, packages/sdk, packages/ui, packages/vscode, packages/web] release v2.0.0
