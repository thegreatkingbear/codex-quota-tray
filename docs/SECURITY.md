# Security Policy & Threat Model (MVP)

## Security claims we can make — only after verification
- App does not directly read/store ChatGPT or Codex credentials.
- App connects to *user-installed Codex CLI* through local stdio instead of an author-operated server.
- App does not collect telemetry, upload crash reports, send analytics, or make its own arbitrary HTTP calls.
- App does not inject into Explorer or require administrator elevation.
- Software is source-visible and buildable by users; public release binaries are traceable to the source revision.

**Do not claim:** '100% safe', 'read-only permissions guaranteed by OpenAI', 'no network traffic' (Codex must contact OpenAI), 'security audited', 'reproducible binaries' (unless achieved and measured), 'all Windows apps are sandboxed', or 'the app cannot access user files'.

## Threats and mitigations
| Threat | Possible consequence | Controls / acceptance evidence |
|---|---|---|
| Malicious build/executable | Steal auth/session data, run arbitrary user-level code | GitHub source; build provenance; pinned dependencies; review; optional code signing, SHA-256 |
| Direct `auth.json` access | Credential compromise | Explicit ban; static grep/check and manual audit |
| Accidental raw message logging | Exposure of IDs, tokens, sensitive content | Safe error-code logging only, no stdout/stderr dumps; tests |
| Exfiltration from application | Account/profile/usage data leak | No project backend/telemetry/HTTP client; inspect outbound connections on Windows; document legitimate Codex traffic |
| PATH substitution | Running attacker-supplied `codex` | Resolve executable to path; test provenance; show path in settings; support user selection; no shell invocation |
| Protocol field tampering | UI displays incorrect/stale quota | Validation/clamps and explicit Unknown, stale timestamps, schema tests |
| Child-process abuse/denial | Resource exhaustion, app hang | Bounded buffers, timeouts, kill owned process on exit, backoff, single instance |
| Silent auto-update compromise | Untrusted binary execution | No auto-updater; manual releases only |
| Taskbar injection | Broad Explorer attack surface | No injected DLL or Windhawk mod |
| User-level app power | Access to files belonging to user | No claim of OS-level confinement; minimize dependencies/rights, explain residual risk |

## Threat boundaries
The UI/transport split is for modularity and reviewability, **not OS-enforced isolation**. Invoking a powerful CLI only for a read operation is a behavioral commitment of this source implementation, not a genuine reduced-privilege token. Running as a normal desktop user is lower risk than elevation but still grants meaningful user-level capabilities.

## Data handling
- **Credentials:** no prompt, save, log, file read, registry access, or clipboard use for secrets. No `auth.json` access.
- **Quota values:** hold primarily in process memory; no history/cache persisted in v1. Logs do not include raw usage snapshots. Config contains only non-sensitive settings.
- **Network:** app itself does not call its own servers. Only the user-installed Codex CLI process can communicate with OpenAI service as necessary; distinguish these processes in diagnostics.
- **Errors:** create fixed error-code enums (`CODEX_NOT_FOUND`, `AUTH_REQUIRED`, `TIMEOUT`, `PROTOCOL_UNSUPPORTED`, `SERVER_ERROR`, `INVALID_QUOTA`, etc.). Show clear remedies; do not show raw stderr/stdout to users.
- **Crash data:** no crash upload, local minidumps by app disabled; remind users Windows OS crash reporting is independently governed by their system settings.

## Mandatory release verification checklist
- [ ] Runs unelevated on clean Windows 11 account.
- [ ] No `auth.json` read anywhere in app or dependencies.
- [ ] No login/token/API key prompts.
- [ ] No project's own HTTP/WebSocket listener and no developer endpoints.
- [ ] Process/network inspection performed and results documented.
- [ ] No direct requests outside allowlisted methods; negative tests pass.
- [ ] Static source scan for `Process.Start` usage; only verified Codex child process and explicitly justified safe Windows actions.
- [ ] Unexpected JSON, long lines, empty/missing bucket, malformed percentages do not crash UI.
- [ ] Code review for third-party package changes, supply-chain vulnerabilities, embedded executables and auto-update mechanisms.
- [ ] SHA-256 release checksums; signing if available; release artifact/source correspondence documented.
- [ ] Terms/trademark/auth integration guidance verified before publishing.

## Vulnerability disclosure
Before publishing repository, add a `SECURITY.md` at the root with maintainer-controlled contact or GitHub private vulnerability reporting. Never publish personal email unless intended. Until then, issues may be used for non-sensitive bugs, but not exploit details or credentials.
