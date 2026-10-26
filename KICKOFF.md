# Codex 시작 지시문 (첫 실행 시 복사해서 사용)

아래 블록을 Codex 프로젝트 첫 대화에 붙여 넣으세요.

---

이 폴더의 `AGENTS.md`를 최상위 지침으로 취급하고 다음 문서를 순서대로 전부 읽어 주세요: `KICKOFF.md`, `docs/PRD.md`, `docs/SECURITY.md`, `docs/ARCHITECTURE.md`, `docs/CODEX_PROTOCOL.md`, `docs/IMPLEMENTATION_PLAN.md`, `docs/ACCEPTANCE_TESTS.md`.

프로젝트는 Windows 11에서 동작하는 독립적인 오픈소스 **Codex Quota Tray** 앱입니다. Windhawk 모드가 아니며, C#/.NET 10 LTS + WPF + Windows Forms NotifyIcon으로 작업표시줄의 시스템 트레이에 실제 Codex 사용량(서버 제공 한도)을 표시합니다.

1. 환경(.NET SDK, Windows 버전, Codex CLI 설치/로그인 상태)을 진단하되 어떤 인증 토큰이나 credential 파일도 직접 읽거나 출력하지 마세요.
2. 공식 문서와 `account/rateLimits/read` 및 App Server의 `initialize`/`initialized` 핸드셰이크가 현재 버전에서 유효한지 검증하고 발견한 차이를 `docs/DECISIONS.md`에 기록해 주세요.
3. `docs/IMPLEMENTATION_PLAN.md`의 **Phase 0과 Phase 1**부터 진행해 주세요. 테스트 가능한 도메인 모델, Mock/Fake provider, 파서 단위 테스트, 앱 기본 골격, 정적인 트레이/팝업을 먼저 구현합니다.
4. 이후 완료된 Phase를 검증한 후 Phase 2 실제 Codex 연결, Phase 3 안정화로 확장해 주세요. 제공된 명세와 충돌하는 변경은 먼저 문서와 이유를 남기세요.
5. 각 Phase 말미에 변경한 파일, 실행한 빌드/테스트 명령, 실제 결과, 아직 검증하지 못한 항목 및 다음 작업을 간결하게 보고해 주세요.

중요: 관리자 권한 금지, 외부 개발자 서버 금지, 텔레메트리 금지, 인증 파일 접근 금지, 사용자 로그인 재요구 금지. 실제 Codex가 없거나 미로그인 상태여도 샘플 모드에서 앱의 UI를 개발할 수 있어야 합니다. `usedPercent`는 사용률이므로 잔여량을 표시할 때만 100에서 빼고, 사용 한도 기간은 서버 응답 값으로 결정합니다.

---

**참고:** 이 ZIP은 지시사항/설계 문서와 테스트용 예시 데이터이며, 아직 실행 가능한 Windows 앱이나 완성된 소스 코드를 포함하지 않습니다. Codex가 실제 프로그램을 구현하도록 하는 입력 자료입니다.
