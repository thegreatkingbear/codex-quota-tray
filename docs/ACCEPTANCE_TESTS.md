# Acceptance Tests — Test Matrix

Testing methodology: automated unit/integration where possible + explicit **real Windows 11** manual tests. Mark unavailable items 'NOT TESTED', never 'PASS'.

| ID | Scenario | Expected outcome |
|---|---|---|
| T01 | Start app on Windows 11 normal user | Appears in system tray; no UAC prompt; optional overflow instructions |
| T02 | Click tray icon; right-click | Popup and context menu work; Refresh/Settings/Exit are functional |
| T03 | Mock mode without Codex installed | App launches with visible 'Sample data' labeling |
| T04 | Codex not on PATH / untrusted path | Clear not-installed state, no arbitrary executable launched |
| T05 | Codex exists but not logged in | Sign-in guidance using official CLI, no password/token prompt |
| T06 | Valid primary/secondary response | Correct remaining=`100-used` and reset timestamp/local conversion |
| T07 | Multi-bucket `rateLimitsByLimitId` | No duplicate legacy bucket; all valid values shown with labels |
| T08 | `secondary=null`, missing/unknown fields | Unknown or omitted values; no exception, no fake zeros |
| T09 | `usedPercent=0,100,101,-1`, string/NaN | 0/100 valid, invalid values show Unknown, not clamped silently |
| T10 | Non-5h/7d duration (15m, 60m, 2d) | Displays actual duration, never labels it 5h/7d |
| T11 | `resetsAt` Unix seconds / timezone & DST | Correct time and safe countdown formatting; injected clock tests |
| T12 | Timeout/EOF/process crash/malformed line | Stable UI error + safe retry, no hang |
| T13 | Continuous process stdout or oversized JSON line | Bounded memory, safe error handling, no freeze |
| T14 | Refresh clicked repeatedly | No overlapping rate-limit requests, rate-limited manual refresh |
| T15 | Sleep/resume, network reconnect, Explorer restart | Correct recovery behavior, icon recreated if needed |
| T16 | User exits app | Icon disposed, own Codex child process terminated, no orphan |
| T17 | Launch at Windows sign-in OFF by default | No automatic run until user explicitly opts in |
| T18 | One app instance already running | No duplicate icons/processes |
| T19 | 100/150/200% DPI and taskbar at different display | Popup visible, readable, keyboard usable |
| T20 | Negative source and runtime check | No `auth.json` access, token logging, browser scraping, injected code, telemetry/backend/HTTP listener |
| T21 | Outbound network observation | App itself does not exfiltrate; separately document official Codex service network traffic |
| T22 | Allowed RPC surface | Runtime mock asserts only initialize, initialized, account/rateLimits/read; no mutating methods |
| T23 | Non-ChatGPT/API-key-only auth | No misleading quota; unsupported/error state if real data unavailable |
| T24 | Version mismatch / field additions | Unknown properties ignored safely, unsupported method handled gracefully |

## Automated tests minimum
- Domain parser tests against fixture + legacy + multi + null + invalid + additional bucket.
- Mock app-server subprocess or test harness to validate protocol handshake, notifications, correlation ID, error, oversized line, timeout and cancellation.
- Security allowlist test with spies recording outgoing method names; fail any addition outside allowlist.
- Formatting tests with fixed `DateTimeOffset`, local-zone translation as injectable dependency.
- Refresh scheduler tests: max one request at a time, bounded retries, stale state.

## Manual smoke test procedure
1. `dotnet --info`, `codex --version`, `where.exe codex` (no credential printout).
2. Start with demo and verify 'Sample data' appears.
3. Start with existing signed-in Codex and compare the displayed percentages/window lengths/reset timestamps with official Codex CLI usage/status or other authoritative UI at about the same time. Expect minor drift due to timing and provider updates.
4. Turn network off, then restore it; observe state and retries. Do not claim every outage means unauthenticated.
5. Exit; inspect process list for orphaned **app-owned** app-server processes.
6. Observe network connections of main app and Codex child separately; record findings without account identifiers.

Record actual platform, test date, SDK, Codex version and PASS/FAIL/NOT TESTED to `docs/TEST_RESULTS.md` (create during implementation).
