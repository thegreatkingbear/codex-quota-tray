# README screenshots

Run from the repository root on Windows with the pinned .NET SDK:

```powershell
dotnet run --project tools/ReadmeScreenshots/ReadmeScreenshots.csproj
```

This renders the production WPF controls and tray glyph renderer into `docs/images`. It uses synthetic quotas, example executable information, and the current time. It does not connect to Codex, read user settings, or change the Windows theme. Images include the app content without OS window title bars. The icon comparison is an enlarged illustration using the real icon renderer; the production demo tray displays `D`.

Inspect all three PNGs after regenerating them and keep the README descriptions in sync with the UI.
