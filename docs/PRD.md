# Product Requirements Document — MVP v1.0

## 1. 문제와 대상 사용자
사용자는 코딩 중 Codex의 서버 보고 사용 한도와 다음 초기화 시각을 확인하려고 자주 CLI/대시보드를 열어야 한다. Windows 작업표시줄의 시스템 트레이에 작은 아이콘과 상세 팝업을 제공하여 이 반복을 줄인다. 개인 개발자/오픈소스 사용자 대상, 계정 정보와 코드의 보호를 최우선으로 한다.

## 2. 사용자 시나리오
- **첫 실행 / Codex 설치됨 & 로그인됨:** 앱이 안전하게 해당 Codex를 사용해 rate limits만 조회하고 결과를 보여준다.
- **Codex 미설치:** 'Codex CLI not found' 및 공식 설치 안내. 패스워드 입력을 요구하지 않는다.
- **미로그인 / 인증 실패:** 'Sign in using the official Codex CLI' 안내. 앱 내부에서 토큰·비밀번호 입력 금지.
- **사용 가능:** 한도 bucket마다 이름/기간/사용 비율/남은 비율/초기화 예정 시간을 표시한다.
- **응답 실패 / 네트워크 단절:** 명확히 stale/offline 표기하고 마지막 성공 값이 있다면 최신 값처럼 표시하지 않는다.
- **샘플 모드:** 인위적인 고정 수치가 항상 샘플임을 배지/툴팁에 표기한다.

## 3. MVP 필수 기능(P0)
1. 트레이 아이콘 표시, 좌클릭 상세 팝업, 우클릭 메뉴(Refresh/Settings/Exit).
2. app-server **stdio** JSONL 연결과 rateLimits read; 현재 서버가 반환하는 복수의 limit bucket 지원.
3. 표시 단위: `usedPercent`(사용 비율), 계산 `remainingPercent=100-usedPercent`(잔여 비율), 분 단위 `windowDurationMins`, Unix seconds `resetsAt`. 누락 값은 'Unknown' 처리.
4. 업데이트: 기본 5분 주기(설정 범위 2~30분), 중복 요청 방지. 실행 직후/사용자 Refresh 때 조회. 실패 시 제한된 지수형 재시도(앱 미작동/미로그인 여부에 따라 백오프 적용). 종료 시 타이머, 자식 프로세스 정리.
5. 상태: Demo / Loading / Connected / Not Installed / Sign-in Required / Offline or Error / Stale / Unsupported. UI에 오류 코드와 복구 행동 명시.
6. 사용자 선택 사항: Windows 로그인 시 자동 시작(기본 OFF), 5/15/30분 조회 주기 등 설정(보안상 민감하지 않은 설정만 저장).
7. 가독성: Windows Light/Dark, 100%·150%·200% DPI, 접근성 텍스트, 색 이외에 수치 병기.
8. 현지 시간으로 reset 표시; 시간대/DST 변화 지원; `resetsAt`은 Unix seconds로 취급.

## 4. 있으면 좋은 기능(P1: v1.1 이후)
- 서버가 제공한 복수 bucket 명칭 변경/필터(식별자가 안전한 값인지 검증) 및 트레이에 표시할 bucket 선택.
- 알림(예: 10% 이하), 기본 비활성화; 반복 알림 방지.
- signed release 및 자동화된 SBOM/보안 스캔.

## 5. 범위 밖
- 일반 ChatGPT 웹 모델별 메시지 한도, ChatGPT 전체 이용 기록, OpenAI API billing/usage, 서버 회계 정보.
- 정확한 '남은 메시지 수'를 임의 추론해 표시하는 기능.
- 트레이 외부에 작업표시줄 내부 텍스트 삽입, explorer.exe injection, 플로팅 오버레이(Phase 2 이후 별도 의사결정 필요).
- 로그 수집 및 전송, 사용자의 프롬프트/저장 파일 읽기, 계정/토큰 관리 기능.
- Workspace owner 이메일 전송, quota reset credit 사용 등 모든 쓰기/변경 RPC.

## 6. 비기능 요구사항
- 로컬 유틸리티로서 일반 사용자 권한 실행, 관리자 권한/백그라운드 서비스 불필요.
- 정상적인 아이들 상태에서 CPU 부하/배터리 영향을 최소화한다. 성능 목표는 실기기 측정 후 기록; 숫자를 검증 없이 마케팅 문구로 쓰지 않는다.
- 프로그램 종료 시 중복 트레이 아이콘, 고아 프로세스, 잠금 파일이 남지 않아야 한다.
- UI에서 연결된 정보는 'last updated' 시각과 함께 표시하여 신선도를 알 수 있어야 한다.
- 설치 프로그램이나 자동 업데이트는 MVP 필수가 아님. 소스 빌드와 별도 실행 파일을 제공할 수 있으나 공개 릴리스 보안 체크 통과 전 배포 금지.

## 7. 수용 기준
`docs/ACCEPTANCE_TESTS.md` 참조.
