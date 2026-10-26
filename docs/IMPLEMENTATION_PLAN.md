# Implementation Plan — Do in order

**Rule:** Implement incrementally; each phase finishes with builds/tests and an updated progress summary. Do not skip security acceptance.

## Phase 0 — Validate, scaffold, baseline
- [ ] Confirm target OS Windows 11, .NET 10 SDK, WPF desktop targeting packs, Codex CLI version/location.
- [ ] Review current official Codex App Server protocol docs from `docs/REFERENCES.md` (initialization, stdio JSONL, `account/rateLimits/read`, field shapes), record findings in `docs/DECISIONS.md`.
- [ ] Create solution and app/core/protocol/demo/test projects; enable nullable, treat warnings reasonably, use pinned SDK via global.json **only if installed/validated**.
- [ ] Add .editorconfig, CI skeleton for Windows build/unit tests, safety-focused `.gitignore`.
- [ ] Ensure project runs without account with sample data.

**Exit:** solution builds at least demo mode, tests bootstrapped, no secrets.

## Phase 1 — Testable domain + tray UI (fake provider)
- [ ] QuotaSnapshot/Bucket/Window domain types with explicit Used/Remaining semantics, freshness.
- [ ] Parser support for legacy single bucket, multi-bucket, missing/null, invalid fields, Unix timestamp seconds.
- [ ] Unit tests for usedPercent conversion, bucket choice, reset formatting, null handling, stale logic, unknown durations, different timezones.
- [ ] Mock fixture provider using `examples/rate-limits.sample.json` (sample data labels mandatory).
- [ ] WPF popup and Windows Forms NotifyIcon (left/right clicks), safe dispose, keyboard/dpi support.
- [ ] Implement error states and mock/connected indicators; no misleading 'real' account status in mock.

**Exit:** fully working visual demo with test pass. Screenshot optional, with 'Demo' watermark if published.

## Phase 2 — Real Codex App Server integration
- [ ] Verified CLI executable selection, no shell invocation, `UseShellExecute=false`; ordinary current user only.
- [ ] Child-process lifecycle, redirected stdin/out/err, cancellation, timeouts, bounded line lengths, safe parser.
- [ ] `initialize` -> wait response -> `initialized` -> `account/rateLimits/read` (method allowlist only).
- [ ] Single-flight refresh scheduler, default 5min polling, exponential backoff, no rapid authentication retries.
- [ ] Map real snapshot and state safely; handle installed/uninstalled/missing auth/unsupported/error.
- [ ] Negative tests assert no other RPC calls. Integration tests with **fake subprocess**; a real Codex smoke test only when user has working login.

**Exit:** real account usage works without asking for credentials and has no direct auth file reads.

## Phase 3 — Polish, security, operational reliability
- [ ] Manual checks Windows 11 x64, light/dark, tray overflow, scaling, Explorer restart, Windows sleep/resume, network loss.
- [ ] Single-instance behavior and child cleanup; startup opt-in if approved/tested; Settings UX.
- [ ] Security review against `docs/SECURITY.md` checklist, validate transport/network by tracing app vs Codex subprocess.
- [ ] Documentation: install/run/troubleshooting, limitations, safe logging, source-build notes.
- [ ] Build artifact release (unpackaged `win-x64` self-contained single-file only if functional tests prove compatible; otherwise framework-dependent). No updater.

**Exit:** acceptance tests complete on real Windows machine; release checklist reviewed.

## Phase 4 — Public distribution (separate decision)
- [ ] Choose MIT or Apache-2.0 with correct copyright holder.
- [ ] Review OpenAI integrations/terms/trademark policy and clarify unofficial status.
- [ ] GitHub Actions reproducible build instructions and checksum, code signing if possible.
- [ ] Source/installed binary network behavior verification and independent review invitation.
- [ ] Publish release notes, vulnerability reporting process, SBOM if desired.

**Exit:** maintainers explicitly approve security & legal release checklist. No claim of independent audit without one.

## Phase 5 — Not yet authorized
Floating taskbar overlay, true taskbar insertion, all-ChatGPT model usage, API billing, automatic updating, history analytics, multi-user accounts, rate-limit-reset credit management. Write a new ADR/PRD before work.

## Implementation status — 2026-10-25

Phase 0 and Phase 1: implemented and verified (Windows builds, first 20 domain/parser assertions, actual demo screenshot).
Phase 2: implemented and verified (74 assertions including fake subprocess transport, signed real CLI smoke and actual quota UI).
Phase 3: reliability/settings/source review implemented; manual acceptance matrix remains partially open. See TEST_RESULTS.md for exact PASS/PARTIAL/NOT TESTED evidence. Optional startup registration omitted; always off. Self-contained local build is multi-file, not a public release.
Phase 4: not started; no publishing, license selection or release approval.
Phase 5: not started.

The original unchecked items above remain the complete specification; this status does not mark unperformed manual checks as complete.
