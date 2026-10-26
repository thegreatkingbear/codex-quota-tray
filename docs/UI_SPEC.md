# UI/UX Specification — Tray First

## Layout & behavior
**Primary interaction:** one Windows notification-area icon (system tray, typically near clock), left-click opens a compact WPF popup; right-click opens context menu. It is not Windows 11's primary pinned taskbar area and the app cannot ensure the icon is always visible (user can configure tray overflow).

### Tray icon
- Normal: compact recognizable monogram/Codex-themed generic mark; optional tiny `remaining` digits only if legible at 16/20/24 px; avoid implying affiliation with official product.
- Status colors: healthy / near limit / exhausted / unknown. Colors never sole communication method; tooltip has textual values.
- 1 selected primary quota window's remaining % as optional visible text; avoid overcrowding. If exact number doesn't fit, use simple ring fill + tooltip.
- Tooltip: `Codex Quota Tray — 5h remaining: 75%; week remaining: 38% · Updated HH:MM` based on **actual** data. Unknown/missing windows are not fabricated. Respect Windows tooltip character limits by truncating safely.

### Click popup
Width approx 320–400 dip, content grows for extra buckets. Compact, accessible, no always-on-top except when open as menu-like flyout.

```text
Codex Quota Tray             [⚙]
Connected via local Codex     Updated 19:26

Codex
5 hours       Remaining 75%  [███████░░░]  Used 25%
Reset         21:40 (local time)
7 days        Remaining 38%  [████░░░░░░]  Used 62%
Reset         Tue 10:00 (local time)

[Refresh]    [Open settings]    [Exit]
```

Values above are *mock UI examples*, never live values.

### States and wording
- Demo: **Sample data — not your account** displayed on tray tooltip and popup.
- Loading: **Checking Codex usage...** progress with no fake percentages.
- Connected: **Updated [local time]**; state comes from successful `rateLimits/read` only.
- Not installed: **Codex CLI not found** — provide official installation instructions; do not install software automatically.
- Sign-in required: **Sign in using Codex CLI, then refresh**; no third-party login.
- Offline/Error: **Unable to reach Codex usage**; button Retry; show safe generic detail code.
- Stale: **Last updated [timestamp] — data may be outdated** with last known values visually muted; upon app restart without previous in-memory value show unknown.
- Unsupported: **This Codex version doesn't provide usage limits** with troubleshooting docs.

### Formatting rules
- Calculate Remaining from Used; do not call `usedPercent` remaining by mistake.
- Duration labels from `windowDurationMins`; reset from `resetsAt` converted to local time zone. Negative reset countdown becomes 'Reset time passed; awaiting refresh' rather than a misleading future value.
- If multiple buckets returned (`rateLimitsByLimitId`), show all reasonable buckets ordered with `codex` first and derive fallback labels. Use max line length and hide control chars.
- Right-click menu: Refresh now, Show details, Settings, Exit. All actual actions wired.
- Settings first release: Refresh interval 2–30 minutes, Launch at sign-in (OFF default), Demo mode toggle, Optional Codex executable path override with confirmation. Display resolved binary path, not secrets.

### Accessibility
Keyboard focus, Esc dismiss, screen reader names, 150/200% DPI, no color-only indications, readable contrast, local language English first, resources organized for Korean later. No UI freezes during 10s+ network timeout. Windows dark/light theme support optional v1 but must remain legible in either mode.

## Updated detail design (2026-10-25)
The detail view now uses a 400–480 DIP content-aware width and automatic height, capped to the current monitor work area with scrolling for overflow. Windows app Light/Dark theme is followed live; high contrast uses system colors. Fluent controls, restrained cards, large remaining percentages and monospaced timestamps replace the fixed-height plain-text layout. Settings also follows the theme and uses an automatic-height scrollable layout. Theme APIs are the built-in .NET implementation; no third-party UI dependency was added.
