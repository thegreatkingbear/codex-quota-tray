# 아키텍처 — 독립 앱 / 최소 권한

## 제안 스택
- C# 13+ / .NET 10 LTS (실제 SDK 설치 버전 확인)
- WPF desktop popup/settings + `System.Windows.Forms.NotifyIcon` (WinForms interop)
- `System.Text.Json` JSONL parser, `Process` with redirected stdin/stdout/stderr, async streams
- xUnit 또는 MSTest 중 하나(최소 의존성 선호); 단위 테스트 중심
- Windows 11 x64 MVP. arm64은 후속 테스트 후 지원 표기.

## 레이어와 의존성

```text
[WPF popup + settings]        [Windows Forms NotifyIcon]
             \                 /
               [Presentation/ViewModels]
                         |
                    [QuotaService]
                  /         \
          [IQuotaProvider]  [RefreshScheduler]
            /          \
 [CodexAppServer]    [FixtureProvider]
      |                       |
 [stdio JSONL]         [examples/*.json]
      |
[existing Codex CLI -> official OpenAI services]
```

**분리 원칙:** AppServer 프로토콜 파싱과 가공 데이터 모델은 WPF를 참조하지 않는다. Mock 공급자는 동일 인터페이스를 구현한다. UI에는 credential/error raw payload가 도착하지 않는다.

## 솔루션 구조 (구현 중 Codex가 생성)

```text
src/
  CodexQuotaTray.App/        # WPF, tray, popup, settings, entrypoint
  CodexQuotaTray.Core/       # QuotaWindow, QuotaBucket, mapping, stale rules
  CodexQuotaTray.Codex/      # stdin/stdout protocol transport and parser
  CodexQuotaTray.Demo/       # deterministic fixture provider

tests/
  CodexQuotaTray.Core.Tests/
  CodexQuotaTray.Codex.Tests/
  CodexQuotaTray.App.Tests/  # 가능한 UI 로직 unit tests
```

트레이 아이콘: 기본은 1개 아이콘에 중앙 텍스트/색상 등으로 상태 표현, 정확한 세부 수치는 툴팁/팝업에 둔다. `NotifyIcon`에 긴 문자열을 다 넣지 않는다.

## 프로세스 라이프사이클
1. 앱은 per-user mutex로 단일 실행을 보장, 이미 실행 중이라면 기존 팝업 활성화 또는 조용히 종료(기술적으로 가능한 쪽을 검증 후 선택).
2. 사용자 PC에서 Codex CLI 실행 파일을 찾아 신뢰 가능한 경로/버전과 실제 파일 여부를 확인한다. 임의 PATH 실행의 위험을 다루고, 필요 시 UI에서 사용자가 경로를 선택하도록 한다.
3. `codex app-server`를 사용자 권한의 **자식 프로세스**로 실행(창 숨김, `UseShellExecute=false`); 자체적으로 서버 포트를 열지 않는다.
4. `initialize` 요청 → 응답 확인 → `initialized` 알림 → `account/rateLimits/read` 요청 순서.
5. Read loop는 완전한 JSONL 레코드를 안전하게 파싱하고 `id`별 응답을 매칭한다. 예기치 못한 서버 알림/요청은 무시/명시적으로 지원 불가 처리하고 raw 내용을 출력하지 않는다.
6. 사용자 갱신 또는 주기적 polling으로 quota 상태 업데이트. 일정 오류 횟수 초과 시 낮은 빈도로 재시도.
7. Exit/Cancellation 시 모든 task, child process, tray icon, UI 핸들 dispose; 타임아웃 이후 자신의 자식 프로세스만 종료한다.

## 시간/데이터 모델

```text
QuotaSnapshot:
  observedAtUtc: Instant
  source: "Codex App Server" | "Demo"
  state: Available | Stale | ...
  buckets[]:
    id: string?
    displayName: string
    windows[]:
      kind: Primary | Secondary
      windowDurationMinutes: int?
      usedPercent: double?
      remainingPercent: double?  # calculated only when valid usedPercent in [0,100]
      resetsAtUtc: Instant?
```

- Display window labels derived from actual duration; 300m = 5h; 10080m = 7d. 다른 값은 자동 포맷(`15m`, `3h`, `2d`)하고 'weekly' 단정 금지.
- 복수 bucket: `rateLimitsByLimitId` 존재 시 우선 이용, 그렇지 않으면 `rateLimits` 사용. 동일 bucket 중복 표시는 피한다.
- `primary`와 `secondary`는 null일 수 있다. 값 검증 후 UI에 표시하며 malformed JSON은 처리 오류 상태로 전환.
- 서버 통지가 빈번해도 UI 쓰레드로 안전하게 marshal. 시계 변화 / DST 처리.

## 설정 파일
민감하지 않은 옵션(테마, 주기, 시작 여부, 선택 bucket)만 `%LOCALAPPDATA%\CodexQuotaTray\settings.json`에 저장. 토큰, account ID, raw 응답 금지. 사용자 권한 ACL을 상속하고 읽기 실패는 기본값 사용. 창 위치 저장 시 다중 모니터/해상도 변경에도 화면 밖으로 사라지지 않도록 clamp.

## 기술 위험
- WPF+WinForms interop에서 참조/메시지 루프/리소스 dispose를 테스트.
- App Server API 버전 변경 가능성: 프로토콜 버전별 특이점은 provider 내부로 한정하고 공식 문서 갱신 확인.
- Windows hidden tray icons 정책은 앱이 강제할 수 없음: 사용자가 시스템 설정에서 아이콘 노출을 선택하도록 안내.
- 앱이 다른 프로세스와 동일한 사용자 권한인 이상 근본적인 OS 보안 격리는 제공되지 않음. 주장 금지.
