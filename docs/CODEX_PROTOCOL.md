# Codex App Server — Protocol Contract

> Official reference: https://learn.chatgpt.com/docs/app-server
> Code reference: https://github.com/openai/codex/tree/main/codex-rs/app-server
> Verify version-specific behavior before implementation and at release time. **Do not scrape the Codex website or read auth.json.**

## Transport
Start an already installed trusted Codex CLI: `codex app-server` using stdio (default). Use newline-delimited JSON over stdin/stdout. Official documentation notes that the wire format omits the `"jsonrpc":"2.0"` header. Never listen on an HTTP/WebSocket port in MVP.

## Initialization handshake (illustrative)

Client → server:
```json
{"method":"initialize","id":1,"params":{"clientInfo":{"name":"codex_quota_tray","title":"Codex Quota Tray","version":"0.1.0"}}}
```

Wait for response matching `id=1`, and then send notification:
```json
{"method":"initialized","params":{}}
```

Only after this handshake request:
```json
{"method":"account/rateLimits/read","id":2}
```

Note: IDs may be any distinct request IDs. Ignore unsolicited notifications unless explicitly supported; don't mistake them for a response. Set limits for JSON line size and buffer usage (decide/test safe caps).

## Example successful response (synthetic values)

```json
{
  "id": 2,
  "result": {
    "rateLimits": {
      "limitId": "codex",
      "limitName": null,
      "primary": {"usedPercent":25,"windowDurationMins":300,"resetsAt":1893456000},
      "secondary": {"usedPercent":62,"windowDurationMins":10080,"resetsAt":1894060800}
    },
    "rateLimitsByLimitId": {
      "codex": {
        "limitId":"codex",
        "limitName":null,
        "primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":1893456000},
        "secondary":{"usedPercent":62,"windowDurationMins":10080,"resetsAt":1894060800}
      }
    }
  }
}
```

Parser behavior:
- `rateLimitsByLimitId` is **optional** and preferred if present/nonempty. `rateLimits` is a backward-compatible single-bucket view.
- Bucket IDs and label names can vary. Do not hardcode bucket list. Sanitize label length and prevent untrusted control chars in UI/logging.
- `primary` and/or `secondary` may be `null`; windows may contain missing fields.
- `usedPercent` is **used**, not remaining. UI remaining = `100-usedPercent` when numeric and 0 ≤ value ≤ 100; otherwise Unknown. Do not silently substitute 0 on invalid input.
- `windowDurationMins` is source of truth, and may be 15 / 300 / 10080 or other values.
- `resetsAt` is Unix seconds (UTC). Convert to viewer's local timezone for rendering only. Tests use an injected clock, not the computer's actual time.
- `rateLimitReachedType`, credits, planType may appear. Ignore optional credits and account fields in v1; never display guessed entitlements.
- Error responses, timeout, EOF, malformed line, oversized line, unknown auth mode or incompatible method produce distinct safe error codes and remediation copy.

## Notifications / updates
Official docs mention `account/rateLimits/updated`; support it only if the update can be validated and integrated without allowing any other request. For MVP a fixed poll interval alone is acceptable. User-initiated Refresh always fetches anew, subject to reasonable anti-spam throttling.

## Client safety rules
Strict request allowlist; only `initialize` and `account/rateLimits/read` carry request IDs, plus `initialized` notification. **Never** call:
- `account/login/start`, `account/logout`, token refresh endpoints;
- `thread/*` or `turn/*`;
- `account/rateLimitResetCredit/consume`;
- any other endpoint that changes account state, spends credits, sends email or executes commands.

The allowlist reduces accidental misuse; it does **not** make a full-trust Codex CLI process an OS-enforced read-only security boundary.

## Login and distribution cautions
The official docs discuss auth method support and potential restrictions for commercial or hosted apps. This is a **local, open-source, non-hosted** desktop utility. Check the current terms and authentication guidance before any redistribution/commercialization. Do not impersonate the official Codex client, present a misleading client ID, or claim OpenAI approval.
