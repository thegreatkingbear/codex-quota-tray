# Architecture Decision Record / Open Questions

## ADR-001 (accepted) — Independent Windows app instead of Windhawk
**Decision:** Standard per-user C# desktop process. No DLL injection into Explorer or system-level taskbar modifications.
**Reason:** Simplifies OS isolation, update and audit surface; tray integration is a supported Windows concept.
**Tradeoff:** Windows 11 may hide the icon in tray overflow; cannot embed arbitrary text in the taskbar's normal icon area via a documented Windows API.

## ADR-002 (accepted) — WPF + Windows Forms NotifyIcon
**Decision:** WPF for popup UI, WinForms NotifyIcon for tray.
**Reason:** Native APIs, minimal dependencies, C# ecosystem.
**Risk:** WPF + WinForms interop and icon lifecycle require Windows test coverage.

## ADR-003 (accepted) — Existing Codex CLI via stdio App Server
**Decision:** Launch installed CLI as child process and use `account/rateLimits/read` with JSONL; no token read, no own auth.
**Reason:** Avoid direct credential handling and web scraping.
**Limit:** Local app server may have more capabilities than the subset we call; it is not a true OS-enforced read-only credential delegation.

## ADR-004 (accepted) — Mock mode first, real integration second
**Decision:** All UI can be built from synthetic fixture; sample values must be clearly marked.
**Reason:** Safe, reproducible automated tests independent of a developer's account.

## ADR-005 (accepted) — No quota data history persisted in MVP
**Decision:** Retain snapshots only in process memory, persist only non-sensitive settings.
**Reason:** Minimize stored personal usage metadata and debugging disclosure.
**Tradeoff:** On app restart, prior quota history is unavailable.

## ADR-006 (open) — App display name / license
**Candidate:** `Codex Quota Tray` / MIT or Apache-2.0.
**Action:** Before publishing, owner chooses final name/license and checks project/brand conflicts. This is not affiliated with OpenAI.

## ADR-007 (open) — Installation and update mechanism
**Default:** Manual download, verified hashes, no auto-updater.
**Action:** Owner can later approve package/signing distribution after security review.

## ADR-008 (open) — Always-visible overlay
**Not in MVP.** Independently floating overlay over taskbar might be possible without injection, but has DPI/auto-hide/window-manager complications. New threat model and ADR required.

## Maintenance convention
After any material deviation, append a numbered ADR with status, decision, alternatives, risk and tests. Do not quietly overwrite accepted security requirements.

## ADR-009 (accepted, 2026-10-25) — Verified toolchain and protocol
Windows 11 Enterprise build 26200; Codex CLI 0.164.0-alpha.11.1, valid OpenAI OpCo Authenticode signature. No system .NET SDK existed. Microsoft SDK 10.0.403 was downloaded into ignored .tools/dotnet and checked against Microsoft's SHA-512 release metadata. global.json pins that validated version.
Official https://learn.chatgpt.com/docs/app-server confirms JSONL stdio, initialize/initialized, account/rateLimits/read, optional multi-bucket map, nullable windows, and Unix seconds. No schema conflict found. Current pricing says Pro has no five-hour cap; no plan-specific duration assumptions are encoded.

## ADR-010 (accepted) — Process per refresh and bounded transport
Each poll owns a short-lived child process, performs the handshake/read, and terminates its process tree. This trades small startup overhead for simpler bounded lifecycle and no idle unsolicited stream. Limits: 20-second operation deadline, 256 records per expected response, 256 Ki characters per line, 1 Mi characters stdout total and stderr total, JSON depth 32, 64 quota buckets. Stderr is discarded, never logged. Only three allowlisted method shapes can be serialized. A late/mismatched response or unsolicited server request is ignored, never acted upon.

## ADR-011 (accepted) — Executable provenance
Auto-discovery is limited to the official desktop install root and requires Windows Authenticode verification with an OpenAI publisher. Verification is cache-only and does not initiate certificate network requests. No PATH or shell script execution. Other installations require explicit executable selection with a displayed SHA-256 trust confirmation. Hash is checked on every poll while holding a file handle that denies write/delete sharing. The trust decision and file hash are not credentials. Updates that replace a selected binary require selecting/trusting it again. This is not OS sandboxing or protection against an attacker already controlling the user's account.

## ADR-012 (accepted) — Dependency-free test harness
Use an executable assertion harness instead of xUnit/MSTest packages. This is a deliberate deviation from the suggested test framework to keep all projects free of third-party packages. The harness exits nonzero on failure and is run explicitly by CI with dotnet run; dotnet test alone does not run these tests. Fake-server mode is test-only and launches a real subprocess to exercise pipes, EOF, hangs, floods, handshake and cleanup.

## ADR-013 (accepted) — Local development build and optional features
Startup registration is deferred and always off; no autorun registry changes. Theme follows system WPF colors, with explicit text and numeric labels. Public distribution remains a separate decision. Produce a self-contained multi-file local build so no system runtime installation is necessary; do not claim single-file or reproducible release verification. No license or endorsement is invented.

## ADR-014 (accepted) — Detail window and tray summary
The tray glyph shows the minimum known remaining percentage rounded down; a tooltip/detail view provides actual per-window values. D means demo; ? means stale/unavailable. No missing value contributes a fake zero. Detail view is a normal non-topmost window with a taskbar entry only while shown, allowing keyboard/task switching and desktop accessibility inspection; X/Esc hides it and leaves the tray process running. This is a deliberate adjustment from a dismiss-on-focus-loss flyout. Rendering does not rebuild focused controls every timer tick. Full multi-monitor/DPI and Explorer-restart validation remains open.

## ADR-015 (accepted) — Square quota border and larger digits
User requested more legible tray digits and a graphical remaining/total indicator. Replaced the circular glyph with a full square dark background, tightly fitted bold white glyph outlines, and a thin square progress border. The border starts at top center and advances clockwise by the actual minimum known remaining percentage; the integer text is still rounded down. Unknown, stale, loading and demo states show no quota progress, preserving truthful status. Native 16/20/24/32/40/48/64px PNG frames are packed into an ICO to avoid a single-size resampling penalty. The Windows tray slot itself is not enlarged. A synthetic preview at native 16px/32px and enlarged 16px was rendered and visually inspected for 100, 79, 50, 9, 0, unknown and demo states; icon construction and 16px loading succeeded. This does not replace full multi-monitor DPI validation.

### Tray spacing adjustment
Increased the border from 1px to 2px at 16px (2px to 3px at 32px), with a dedicated dark gutter around the digits. At 16px, digit outline height changes from 10px to 9px, limiting the reduction to 10%; at 32px it changes from 24px to 22px. Native-size and enlarged synthetic previews were visually checked before applying. The tray slot size and quota semantics are unchanged.

## ADR-016 (accepted) — Opt-in startup at Windows sign-in
User requested a startup option. This supersedes the earlier startup deferral. Settings now has a default-off checkbox; Save adds/removes only the CodexQuotaTray REG_SZ value under HKCU\Software\Microsoft\Windows\CurrentVersion\Run. The quoted executable path includes --startup, suppressing the details window at sign-in. No service, elevation, scheduled task, shell wrapper or HKLM writes. Registry registration is the source of truth; no startup flag is duplicated in settings.json. We do not override Windows StartupApproved state or auto-recreate entries on app launch. An externally disabled startup app may need enabling in Windows Settings. Paths are fully qualified and quoted; dotnet-host, non-EXE, missing, malformed and over-260-character commands are rejected. Moving the app requires disabling/re-enabling the option. Settings can open even when the details window has never been shown.
Reference: https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys
Verification: 14 new assertions exercise the production registry adapter in a unique temporary HKCU test key (not the real Run key), including default-off, registration, persistence, quoting, removal, unrelated-value preservation and invalid paths. All 88 assertions passed. Real Windows sign-out/reboot is not performed in this session.

## ADR-017 (accepted) — Counterclockwise remaining and three quota colors
User requested reversing the remaining indicator so consumed gray grows clockwise from 12 o'clock. Remaining now fills counterclockwise from top center. Color uses the actual remaining percentage: >50 green, 20–50 yellow, <20 red. Unknown/stale/demo states still have no colored quota fill; zero remaining has an empty gray track. Digit size, border thickness and gutter are unchanged. This supersedes the clockwise remaining behavior in ADR-015. Native 16px/32px previews were visually inspected at 100, 75, 50, 20, 9, 0 and unknown.

## ADR-018 (accepted) — Content-sized Fluent details and system theme
Replaced the fixed-height detail window with content-driven height and monitor-work-area limits. Width starts at 400 DIPs and can expand for bucket labels up to 480 DIPs, constrained by the target monitor; longer text wraps. Excess content scrolls instead of escaping the screen. Height changes keep the window anchored to the selected monitor's bottom-right work area. Settings also uses content-driven height with a scroll cap.
The detail view is separated into DetailsView: clear status text, quota cards, prominent remaining percentages, monospaced figures/timestamps, per-quota remaining bars, timestamp and compact actions. Color does not replace numeric or status information. Existing tray, data and authentication behavior is unchanged.
Use the SDK's built-in Fluent Application.ThemeMode.System for native controls/title bars; this API carries WPF0001 in .NET 10, so the suppression is localized and the SDK remains pinned. Custom dynamic brushes follow the read-only AppsUseLightTheme preference; user-preference/system accessibility change notifications refresh them on the UI dispatcher. High contrast uses Windows system brushes. No OS theme setting is modified. Event handlers are unregistered on shutdown.
Reference: https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.thememode?view=windowsdesktop-10.0

## ADR-019 (accepted) — General/advanced settings and developer-tool actions
Settings is now a dedicated SettingsView with prominent current-user startup and refresh interval controls, an explanation of the unchanged minimum-remaining tray rule, and a default-collapsed Advanced section for demo mode, executable path, SHA-256 and troubleshooting. Read-only path/hash fields allow selection without editing the trusted selection. Save/Cancel remain outside the scrolling area; Cancel/Esc discards edits. Expanding content resizes within monitor bounds and scrolls above the fixed actions. Selection and save/auth/startup safety behavior are unchanged.
Detail actions now use a compact 28-DIP IDE-style toolbar with built-in monochrome Windows icons, short labels, tooltips, hover/pressed treatment and visible keyboard focus. Refresh and Settings remain directly available; Exit moves to the More actions (...) menu and remains in the tray context menu. Settings shares the compact action style with a subtle border on Save. No external icon package or runtime dependency is added.
References: VS Code Panel Toolbar (https://code.visualstudio.com/api/ux-guidelines/panel) and Editor Actions (https://code.visualstudio.com/api/ux-guidelines/editor-actions): useful tooltips, restrained action count, secondary actions in overflow; adapted to this standalone WPF application, not claimed to be a VS Code extension.

## ADR 020 — User-selectable tray quota
User-requested follow-up promotes the PRD's deferred tray bucket selection into current scope. Default is shortest server-reported positive duration (5h before 7d), not smallest remaining or closest reset timestamp. Unknown periods sort last; ties sort by stable key. Settings enumerates all received windows and preserves a missing saved choice, falling back automatically until it returns. Missing percentages remain unknown on the chosen window. A SHA-256 key of quota identity, window kind and duration is saved; no usage values, reset timestamps or account identifiers are persisted. Parser retains only a digest of the quota map key/legacy limitId. Changing display settings updates the existing snapshot immediately without an extra server request. Demo/stale/loading safeguards remain unchanged.

## ADR 021 — Authorized public preview release
The owner explicitly requested a downloadable public release and selected MIT. This authorizes a v0.1.0 prerelease with disclosed verification gaps, superseding prior local-only distribution statements; it does not mark the stable-release acceptance matrix complete. No independent audit, clean-account verification, legal certification, or code signing is claimed. The package contains the application, Microsoft runtime, notices, source commit/build metadata and checksums, never Codex itself or user settings. Source changes including the README screenshots are committed before packaging. The existing security checklist remains the stable-release gate.
Official app-server documentation and OpenAI brand guidelines were reviewed on 2026-10-25: https://learn.chatgpt.com/docs/app-server and https://openai.com/brand/. The documented read endpoint remains supported. Branding uses a custom quota icon and explicit independent/not-endorsed text; this review does not claim trademark permission or legal approval. Only the existing three allowlisted methods are used.

## ADR 022 — Fixed-folder installation and manual package updates
User requested preventing duplicate Windows tray records across releases. Starting v0.1.1, the release ZIP has Install.cmd/Install.ps1 and an app subfolder. The installer always targets current-user LocalAppData/Programs/CodexQuotaTray and creates one Start menu shortcut. Startup is migrated only when already opted in, and StartupApproved is never overridden. Settings stay outside the binary directory.
The Windows PowerShell 5.1-compatible installer validates manifest paths and hashes, rejects reparse points and unlisted files, stages payloads before swapping directories, and retains previous files. Users exit the app first; no force-kill or automatic download is performed. Identified old notification keys are exported before leaf-only removal; current-path and unrelated records are preserved. This uses Windows' existing notification preference store, not Explorer injection. Installer scripts use a process-scoped execution policy without changing persisted system policy. Prior no-installer statements are superseded; the no-network-auto-updater constraint remains.
