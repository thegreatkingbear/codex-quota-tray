# Official References (reverify during development)

Checked at project preparation on 2026-10-25. External documentation and terms may change.

1. **Codex App Server official protocol** — https://learn.chatgpt.com/docs/app-server
   - `codex app-server` transports, `initialize` + `initialized`, `account/rateLimits/read`, bucket fields (`usedPercent`, `windowDurationMins`, `resetsAt`), `rateLimitsByLimitId`.
2. **OpenAI Codex open-source App Server** — https://github.com/openai/codex/tree/main/codex-rs/app-server
   - Source to inspect when protocol or runtime behavior differs by version.
3. **Microsoft .NET 10 NotifyIcon API** — https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=net-10.0
   - Standard notification area / system tray icon support.
4. **Microsoft WinForms NotifyIcon guide** — https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-windows-forms
   - Usage and constraints for tray icons.
5. **Microsoft WPF documentation** — https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
   - Native Windows presentation framework.
6. **.NET releases and support** — https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
   - Confirm .NET 10 SDK/support at implementation time.

**Protocol caution:** The App Server docs describe stdio as supported transport; WebSocket listener mode is experimental and not supported for production workloads. This project uses **stdio** only. Codex server may make network calls to OpenAI; the app itself does not need an author-managed backend.
