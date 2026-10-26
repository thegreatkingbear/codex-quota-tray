# Codex Quota Tray — Agent Instructions

> This file is authoritative for coding agents working in this repository. Read this file first, then `docs/PRD.md`, `docs/SECURITY.md`, `docs/CODEX_PROTOCOL.md`, and `docs/IMPLEMENTATION_PLAN.md` before modifying files.

## Mission
Build an **independent, open-source, Windows 11 desktop app** that shows the signed-in **Codex** account's actual server-reported usage limits in the **Windows notification area (system tray)**, with a click-to-open popup showing each quota bucket and reset time.

This project is **not** a Windhawk mod, browser extension, ChatGPT web message-counter, or OpenAI API billing tracker. Version 1 is a normal per-user desktop process and must not modify or inject code into `explorer.exe`.

## Non-negotiable constraints
1. **Stack:** C# / .NET 10 LTS; WPF UI; Windows Forms `NotifyIcon` for system tray. Default framework `net10.0-windows`; build/test on Windows 11. Prefer built-in libraries and zero third-party runtime dependencies.
2. **Least privilege:** Never require administrator elevation, a Windows service, a kernel driver, DLL injection, or browser session/cookie scraping.
3. **Authentication:** Never ask the user to paste a ChatGPT password, API key, OAuth token, or `auth.json` content. Never open/read/copy `~/.codex/auth.json` or any credential store directly. Leverage an existing, trusted, installed Codex CLI login through its app-server protocol.
4. **Transport:** Spawn the user's verified `codex app-server` child process and use **stdio JSONL**, not a network listener. No backend controlled by this project's authors; no telemetry, ads, analytics, crash upload, updater, or arbitrary network calls. Codex itself may communicate with OpenAI to retrieve current limits; communicate this accurately.
5. **Method allowlist for v1:** `initialize`, `initialized`, `account/rateLimits/read`; optionally receive `account/rateLimits/updated` and other notifications without acting on them. Do not call login/logout, thread/turn, credit consumption, token refresh, workspace email or any mutating endpoint. Implement a strict central method allowlist and automated test.
6. **Truthful data:** Display `100 - usedPercent` only when a valid percentage is returned. Display the actual server-provided `windowDurationMins` and `resetsAt`. Never assume quotas are always 5 hours / 7 days; support optional buckets and `rateLimitsByLimitId`. Never invent quota values, reset times, or a 'connected' state.
7. **Security visibility:** Never log raw app-server responses, stdout/stderr lines, account IDs, user prompts, or environment variables. Whitelist safe diagnostic codes only. No sensitive persistence in v1.
8. **Offline-first UI:** App remains functional in demo mode with synthetic fixtures; distinguishes disconnected, missing Codex, unauthenticated, refreshing, stale, unsupported, and available states.
9. **Releases:** No automatic download-and-execute mechanism. Document source builds, hashing, reproducible-build limitations, and release provenance. Do not claim the product is independently audited or endorsed by OpenAI.
10. **Scope discipline:** MVP only: tray icon, popup, refresh, settings (minimum), auto-start optional and opt-in, graceful exit. Floating widget/taskbar overlay, multi-account, universal ChatGPT usage, credits redemption, arbitrary API integrations are **out of scope**.

## Coding and work rules
- Complete phases sequentially from `docs/IMPLEMENTATION_PLAN.md`; do not skip tests, do not make speculative API assumptions.
- Before implementing the app-server integration, verify the currently installed Codex version and the **official docs** linked in `docs/REFERENCES.md`. Update docs/decision log if they conflict. If Codex is unavailable, implement and test with fixtures first rather than reading auth files.
- Separate protocol parsing and domain models from WPF controls. Keep UI and transport behind interfaces to facilitate testing. Dependency injection may be hand-rolled; avoid heavy containers.
- Build Windows app and run tests whenever the execution environment supports .NET desktop. If not available, say so and still run non-Windows unit tests for parsing/business logic where possible. Never claim Windows UI was verified without testing it on Windows.
- Protect against child-process stdout floods, hangs, JSON parse failures, stale data, process exits, concurrent refreshes, and cancellation. Do not echo raw protocol data to UI or logs.
- Implement single-instance behavior per Windows user; no global mutex unless justified. Use stable cleanup of the tray icon and child process.
- Make startup-with-Windows opt-in. If configured, run with ordinary user permissions and provide a disable control.
- Prefer short, reviewable commits and update `docs/DECISIONS.md` when choosing architecture/security approaches.

## Definition of done for MVP
All measurable acceptance criteria in `docs/ACCEPTANCE_TESTS.md` pass on Windows 11. The security checklist has no unresolved critical/high items. A clean user can run mock mode without credentials and see clearly labeled sample values. A user with installed and signed-in Codex can see correct server-reported limits without supplying tokens to this app. The program exits cleanly without leaving child processes or tray icons.

## First action for Codex
Read `KICKOFF.md` and execute Phase 0 → Phase 1. Do not begin with floating overlays, cosmetic polish, GitHub publishing, or assumptions about a fixed 5-hour / weekly window.
