# Test results — local development build

Test date: 2026-10-25 (America/Los_Angeles). Windows 11 Enterprise build 26200, x64. .NET SDK 10.0.403, Desktop runtime 10.0.14. Installed Codex CLI 0.164.0-alpha.11.1; valid OpenAI OpCo Authenticode signature. Runs asInvoker, no elevation requested. This directory was not initialized as a Git repository; no source revision or public release provenance is claimed.

## Phase 0 and 1

- PASS: solution/core/protocol/demo/app/test projects built on Windows with zero warnings/errors.
- PASS: first 20 parser/domain assertions, synthetic fixture, percentage inversion, missing/invalid values, multiple buckets, variable durations, Unix seconds, timezone and freshness behavior.
- PASS: self-contained demo launched on Windows; visible Sample data label, both fixture windows, progress bars and local reset times verified using screenshot and accessibility tree.
- Environment detail: shell-launched UI was not targetable from the interactive desktop tool. The app was subsequently launched via the desktop UI tool and actually inspected. Build success alone was not counted as visual verification.

## Phase 2

- PASS: 74 automated assertions in Release, using `dotnet run --project tests/CodexQuotaTray.Tests -c Release` (not dotnet test).
- PASS: fake subprocess handshake verifies initialize -> initialized -> account/rateLimits/read and request/notification shapes; negative method allowlist checks.
- PASS: notifications and unrelated IDs ignored; malformed JSON, EOF, timeout, excessive line/record counts, stderr flood, cancellation produce safe failures. Child exit verified in all fake subprocess cases.
- PASS: single flight, manual debounce, capped exponential backoff, stale retention, no fabricated connected state, DST conversion, name sanitization, empty-map fallback, executable hash substitution blocked before execution.
- PASS: explicit real CLI smoke test returned LIVE_RATE_LIMITS_READ using only permitted messages. Payload was parsed in memory and never printed/persisted.
- PASS: real app auto-discovered signed Codex, retrieved actual quota and displayed it. Displayed period, used/remaining percentage and reset matched the Codex desktop usage-limit tool at the time of comparison. Account data and actual usage snapshot are intentionally not copied into this report.

## Phase 3 checks performed

- PASS: detail window, Settings, Demo checkbox, Save, actual-to-demo and demo-to-actual transitions, and enabled/disabled Refresh states inspected on Windows.
- PASS: normal Exit used through the UI; subsequent publish could replace binaries. In-flight cancellation separately exercised by fake subprocess test.
- PASS: duplicate launch test: 1 instance before, 1 after; second process exited within 5 seconds.
- PASS: settings persisted only IntervalMinutes, Demo, ExecutablePath, ExecutableHash. No usage or credential cache.
- PASS: static scan of production sources: no HTTP/WebSocket/listener/client APIs, auth-file access, raw Console output, telemetry, registry autorun, or updater. One Process.Start site for explicit Codex executable; UseShellExecute=false, redirected pipes, hidden child.
- PASS (bounded observation only): 25-second Windows TCP connection sampling during a real refresh saw zero main-app TCP endpoints, one owned Codex child with TCP endpoints, and zero owned Codex children remaining afterward. No addresses, response lines, prompts or account IDs were saved. This is not exhaustive packet tracing or proof of no network use under every condition.
- PASS: no third-party PackageReference dependencies. Production uses .NET/WPF/WinForms and Windows signature APIs. Local self-contained artifacts include Microsoft runtime components.

## Acceptance matrix

| IDs | Result | Evidence / limitation |
|---|---|---|
| T01 | PARTIAL | Windows app runs without UAC; clean-account run not tested |
| T02 | PARTIAL | Detail/Settings/Refresh/Exit tested; actual tray left/right click and overflow not manually exercised |
| T03 | PASS | Embedded fixture works with no provider call; Windows sample UI verified |
| T04 | PARTIAL | Missing provider/hash substitution tests; auto-discovery succeeded; clean machine not tested |
| T05 | PARTIAL | Fake authentication failure tested; real account was already logged in |
| T06–T11 | PASS | Parser/domain/DST tests; actual display cross-check |
| T12–T14 | PASS | Fake process + service concurrency/backoff tests |
| T15 | NOT TESTED | Sleep/resume, real network outage/reconnect and Explorer restart not exercised |
| T16 | PARTIAL | UI Exit and fake-process cleanup verified; forced OS crash/logoff not tested |
| T17 | PASS | No startup registration implementation; always off |
| T18 | PASS | Second instance exits |
| T19 | PARTIAL | Current monitor screenshot and accessibility labels inspected; full 100/150/200% multi-monitor matrix not tested |
| T20 | PARTIAL | Static source review passed; no comprehensive filesystem trace or independent audit |
| T21 | PARTIAL | Bounded process TCP sampling passed; full network trace pending |
| T22 | PASS | Central allowlist and negative tests; fake runtime handshake |
| T23 | PARTIAL | Unsupported provider states tested; real API-key-only account not tested |
| T24 | PASS | Extra fields ignored; unsupported method, invalid shape tests |

No known critical/high source-review finding remains, but the mandatory public-release checklist is **not complete**. Phase 0/1/2 implementation is verified; Phase 3 has the explicit manual gaps above. Public distribution, license choice, signing, terms/trademark review, and independent review are not performed.

## Tray readability revision
The circular icon was replaced by large white digits on a square, with a clockwise remaining-percentage border starting at top center. Native-size 16–64px icon frames are included. Synthetic 0/9/50/79/100/unknown/demo renderings were visually inspected at 16px and 32px; native icon construction/16px selection was exercised. No new business logic test was introduced for this visual-only change; the existing 74 assertions and Windows Release build are rerun via build.ps1 -Publish. All earlier manual validation limitations remain.

## Opt-in startup revision
Supersedes earlier statements that autorun is not implemented. Settings now supports opt-in HKCU Run registration and removal; default remains off. Production registry code was tested in an isolated temporary HKCU key, with cleanup, without enabling real sign-in startup during tests. 14 additional assertions bring the Windows suite to 88. Real sign-out/reboot remains NOT TESTED. No credentials or usage records are written; only the executable command is registered. Earlier static-review statements about no registry autorun apply to the prior build.
Settings UI was also inspected on Windows after publication: the Korean startup checkbox is visible and unchecked by default, with Save and current-user/tray-only guidance readable. The updated app was left running with Settings open; automatic startup was not enabled on the user's behalf.

## Remaining direction and color revision
Reversed remaining fill to counterclockwise from 12 o'clock; gray consumed portion now increases clockwise. Green >50%, yellow 20–50%, red <20%, no colored fill for unknown/stale/demo. Native 16px and 32px previews at 100/75/50/20/9/0/unknown were visually inspected. Windows Release build completed with zero warnings/errors, all 88 existing assertions passed, and the updated app was relaunched. Digit dimensions and spacing are unchanged.

## Content-sized, themed details revision
Windows Release build passed with zero warnings/errors; all 88 automated domain/transport/startup assertions passed. A separate local WPF rendering harness exercised the production DetailsView and DesktopTheme palette in both Light and Dark modes, without changing the user's Windows theme. Synthetic single-window layout measured 400x498 DIPs, two-window and 12-bucket layouts respected the 650-DIP test cap and scrolled, and an error-only state shrank to 400x301. Light/dark single, many and error PNGs were visually inspected. These are renderer checks, not all-monitor OS DPI certification. Real live details were inspected via Windows screenshot/accessibility: card layout, actual percentage, reset timestamp, controls and content-fit height appeared correctly. Full live OS-theme switching/high-contrast/multi-monitor testing remains pending.
The live Windows UI was additionally switched from the real single-window quota to the two-window demo and back. The detail window visibly expanded for two windows and shrank for the loading state; Settings controls and Save worked with Fluent styling. Demo mode was restored to off after verification. No Windows theme or startup preference was changed during these checks.

## Settings and action-toolbar revision
General settings now highlight startup and refresh interval; executable path/hash and demo controls live in default-collapsed Advanced settings. Save/Cancel remain visible outside the scroller. Details uses compact icon+label Refresh/Settings actions and a More actions menu for Exit. Light/dark WPF renderings were inspected for the updated details and collapsed/expanded settings; interval selection and default-collapsed state were checked by the local rendering harness. The core tray rule is unchanged: minimum valid remaining percentage across all returned windows, floored for numeric icon display, not a fixed primary/secondary or plan choice.

Windows Release publish completed with zero warnings/errors and all 88 assertions passing. The published app was relaunched, and live detail buttons and collapsed Settings were inspected through Windows screenshot/accessibility. Expanded settings were inspected in the rendering harness; a subsequent live inspection could not reacquire the closed/hidden window. Full interactive overflow-menu and keyboard-focus verification remains pending. Startup and demo preferences were not changed.

## Selectable tray quota revision
Windows Release build/publish passed with zero warnings/errors and 100 assertions. Twelve new checks cover shortest-duration selection versus reset/remaining, both explicit windows, missing/empty/null responses, unknown duration and percentage, stable keys across usage/reset changes, multiple quota IDs, legacy settings defaults and serialization roundtrip. Production WPF SettingsView harness verified automatic plus two per-window options and selection binding; light/dark renderings were generated and the light layout inspected. Published app was relaunched and live settings dropdown showed Automatic plus the current server's single period. Multi-period behavior used synthetic fixtures, not a live Plus account. No startup preference was changed.

## English interface revision
All remaining app-owned Korean settings labels, help text, tooltips, and accessibility names were translated into English, along with the full README. Source/README scan found no Korean text remaining. The production WPF rendering harness passed for light/dark and collapsed/expanded Settings, and the English light Settings rendering was visually inspected. Windows Release build/publish passed with zero warnings/errors and all 100 assertions. Native Windows dialogs and server-provided names retain their platform/source language.
