# Open-source / Release Checklist

**First priority:** earn trust with verifiable evidence, not privacy slogans.

## Before first public version
1. Ensure permission/licensing for the project name and API integration. Add disclaimer: **Unrelated to / not endorsed by OpenAI**. Check OpenAI's current integration guidance, terms, and trademark policy; local open-source app does not imply blanket rights for a commercial/hosted product.
2. Decide open-source license (MIT/Apache-2.0 suggested only; owner must choose). Do not ship a license with fictional copyright owner.
3. Publish complete source, test suite, build steps, supported Codex CLI versions and threat model.
4. Review dependencies and create a dependency inventory. Favor standard .NET components.
5. GitHub Actions builds on pinned Windows runners/SDK; archive logs and checksums. Explain that checksum alone does not prove the binary matches source; build provenance and reproducible-build tests strengthen that claim.
6. Sign binaries if signing infrastructure is available; if unsigned, say so clearly and explain how to build from source.
7. No silent updater, remote code download, installer bundling, advertisements, analytics, credential prompts, or surprise startup tasks.
8. Publish `SECURITY.md` with private vulnerability reporting contact/mechanism. Add dependency update policy and maintainer response expectations.
9. Record Windows 11 integration tests, security reviews, network observation and limitations. If not independently audited, explicitly disclose it.
10. Give users a transparent explanation of how Codex app-server stdio communication works and why Codex itself has network access even if the app has no backend.

## Suggested README trust section (only if confirmed by code/tests)
- **No additional login:** uses the official Codex CLI login you already have.
- **No credential access:** does not open or store Codex authentication files.
- **No telemetry or developer servers.**
- **Runs as a standard Windows user.**
- **Open source:** anyone may review and build locally.

## Distribution target
Release a ZIP with Windows executables and SHA256SUMS; also provide source-only installation instructions. Installer/MSIX are optional future deliverables, not launch blockers. Clearly indicate supported architectures, code signing status, and how to quit/uninstall including autorun opt-in removal.

## Product limitations to disclose
- Rate limits and reset time depend on what Codex server reports; quotas change and can be unavailable.
- The app does not show generic ChatGPT web conversation usage or OpenAI API invoices.
- Windows may place the tray icon inside overflow until user chooses to show it.
- App Server protocol may change; integrations should verify compatibility with supported Codex CLI versions.
