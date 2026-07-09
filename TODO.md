# Y-popup TODO

마지막 검토일: 2026-07-09

> 2026-07-09~10: 모든 Phase 완료. 기존 이슈 수정 3건 완료.

## P0 - 릴리스 전 반드시 확인

- [x] 핵심 프로토콜과 경로 안전성 자동 테스트 추가
  - `tests/Ypopup.Core.Tests` 생성, `PacketCodec`·`SharedFolderPathHelper`·`SettingsService`·`FileNameSanitizer` 단위 테스트 31개
  - `ProgressThresholdReporter`·`TransferProgress` 포함

- [x] 공유폴더 기본 동작 자동 테스트 추가
  - `tests/Ypopup.Network.Tests` 생성, `SharedFolderHostService` + `SharedFolderClient` 통합 테스트 8개
  - `/api/list`, `/api/download`, 없는 파일, 경로 탈출 모두 검증

## P1 - 높은 우선순위

- [x] 들어오는 TCP 작업 수 제한
  - `ConnectionLimiter` (SemaphoreSlim max=20) — `TcpHostService`·`SharedFolderHostService` AcceptLoop에 적용
  - 테스트 6개

- [x] 포트 변경 적용 방식 정리
  - `TcpHostService`·`DiscoveryService`에 `StopAsync`/`RestartAsync` public 추가
  - `YpopupCoordinator.SaveSettings`가 포트/IP 변경 시 3개 서비스(TCP/UDP/공유폴더) 일관 재시작
  - 설정 메시지 "재시작 후 적용" → "자동 적용됨"

- [x] 백그라운드 작업 실패 추적
  - `BackgroundTaskTracker` — fire-and-forget 예외를 `Debug.WriteLine` + onError 콜백으로 전환
  - Coordinator 재시작/자동답장 + AcceptLoop fire-and-forget 4곳 적용
  - 테스트 4개

## P2 - 중간 우선순위

- [x] 복잡한 네트워크 환경에서 LAN 탐색 개선
  - `LanDiagnosticWindow` — 선택된 IP, 브로드캐스트 대상, 포트, announce/패킷 시간 경과, 피어 목록 표시
  - `DiscoveryService`에 `LastAnnounceSentUtc`·`LastPacketReceivedUtc` 추가
  - 트레이 메뉴 "LAN 진단" 항목 추가

- [x] 구조화된 로그 추가
  - `LogService` (Ypopup.Core/Logging) — 일별 롤링 파일 + Debug.WriteLine 동시 출력
  - 7일 지난 로그 자동 삭제
  - 기존 `Debug.WriteLine` 13곳 → `LogService.Debug/Info/Warning/Error`로 교체
  - ApplicationHost 시작 시 `%AppData%\Y-popup\logs` 초기화

- [x] 파일 전송/다운로드 진행률과 취소 기능 추가
  - 이유: 큰 파일을 보내거나 받을 때 느린 Wi-Fi에서는 앱이 멈춘 것처럼 보일 수 있습니다.
  - 완료: `PacketCodec`에 `IProgress<TransferProgress>` 오버로드, `TransferProgressBar` UserControl, Compose(송신)·SharedFolder(다운로드) 진행률 바 + 취소 버튼

- [x] 크로스플랫폼 지원 범위 재검토
  - `docs/cross-platform-support.md` 기능별 매트릭스 작성
  - `explorer.exe` 직접 호출 → `UseShellExecute=true`로 수정 (macOS `open`, Linux `xdg-open` 자동)
  - `AppInfo.cs` "Linux" 표기 제거 (미배포)
  - README에 링크 + Linux 미지원 명시

- [x] 배포 산출물 관리 방식 정리
  - 결정: GitHub Releases로 이동. `docs/`는 웹페이지만 유지.
  - `publish.ps1` → binary를 `release/`(gitignore)에 생성
  - `docs/index.html` → GitHub Releases URL로 다운로드 링크 변경
  - `push-github.ps1` → binary git add 제거
  - `tools/create-release.ps1` 신규 — `gh release create` 자동화

## P3 - 있으면 좋은 개선

- [x] 스모크 테스트 스크립트 추가
  - `tools/smoke-test.ps1` — `dotnet build` → `dotnet test` → 프로젝트 구조 확인

- [x] 설정 검증 중복 줄이기
  - `SettingsValidator` (Core/Settings) — 정적 메서드 5개 (DisplayName, Port, PortsDiffer, ShareFolderPath, AwayIdleMinutes)
  - `ValidationResult` struct — `IsValid` + `ErrorMessage`
  - `SettingsEditor.TrySaveAsync` — 중복 검증 로직 제거, `SettingsValidator` 호출로 대체
  - `NetworkSettingsPanel.AddFirewallRuleButton_Click` — 동일
  - `SettingsValidatorTests` 19개 추가

- [x] 앱 내 진단 정보 내보내기 추가
  - `DiagnosticExporter` (Core/Diagnostics) — 설정/OS/네트워크/포트/피어/로그 일괄 내보내기
  - `LanDiagnosticWindow`에 "내보내기" 버튼 → 바탕화면에 `Y-popup-diagnostic-*.txt` 저장

- [x] Avalonia XAML 로더 경고 재발 여부 확인
  - clean 빌드 시 5개 창(Compose/Receive/Settings/SharedFolder/UserList)에서 AVLN3001 재발 — 런타임 문제 없음
  - A(경고 무시) 방침 유지, 기본 생성자 추가 안 함

## 최근 완료

- [x] 레거시 WPF 앱 프로젝트 제거
  - Avalonia 쪽에 트레이, 알림, 시작프로그램 등록, 방화벽 처리, 부재 감지, 주요 UI가 모두 있는 것을 확인한 뒤 `Ypopup.App`을 솔루션과 소스 트리에서 제거했습니다.

- [x] **P0** 핵심 프로토콜·경로·설정 자동 테스트 + 공유폴더 통합 테스트 (2026-07-08)
  - `tests/Ypopup.Core.Tests` 단위 테스트 31개 + `tests/Ypopup.Network.Tests` 통합 테스트 8개
  - `FileNameSanitizer` `Core/IO`로 추출, `SettingsService` internal 생성자로 테스트 격리

- [x] **P2** 파일 전송·다운로드 진행률 및 취소 (2026-07-08)
  - `TransferProgress` 모델 + `ProgressThresholdReporter` (1MB/5% 임계값)
  - `PacketCodec`·`TcpHostService`·`SharedFolderClient`에 `IProgress` 오버로드 (기존 시그니처 유지)
  - `TransferProgressBar` UserControl (`Desktop/Controls`)
  - `ComposeWindow` 송신 + `SharedFolderWindow` 다운로드에 진행률 바 + 취소 버튼

- [x] **P1-1** TCP 동시 접속 제한 — `ConnectionLimiter` + 테스트 6개 (2026-07-08)
- [x] **P1-2** 포트 변경 일관성 — `StopAsync`/`RestartAsync` 공개, Coordinator 3서비스 재시작 (2026-07-08)
- [x] **P1-3** 백그라운드 실패 추적 — `BackgroundTaskTracker` + 테스트 4개 (2026-07-08)
- [x] **P2-C** 크로스플랫폼 지원 범위 재검토 — `docs/cross-platform-support.md` + `UseShellExecute` 수정 (2026-07-08)
- [x] **P3-X** XAML 로더 경고 재발 확인 — AVLN3001 재발, A 방침 유지 (2026-07-08)
- [x] **P2** LAN 진단 화면 — `LanDiagnosticWindow` + DiscoveryService 타임스탬프 + 트레이 메뉴 (2026-07-09)
- [x] **P2** 롤링 로그 — `LogService` (Core/Logging) + 기존 Debug.WriteLine 13곳 교체 + ApplicationHost 초기화 (2026-07-09)
- [x] **P2** 배포 산출물 정리 — GitHub Releases 전환, `publish.ps1` → `release/`, `docs/index.html` URL 변경, `create-release.ps1` 신규 (2026-07-09)
- [x] **P3** 설정 검증 중복 제거 — `SettingsValidator` 신규 (Core) + `ValidationResult` + 테스트 19개 + UI 2곳 적용 (2026-07-09)
- [x] **P3** 스모크 테스트 — `tools/smoke-test.ps1` (2026-07-10)
- [x] **P3** 진단 내보내기 — `DiagnosticExporter` (Core) + LanDiagnosticWindow 내보내기 버튼 (2026-07-10)
- [x] 기존 이슈 수정 — .gitignore(IDE 패턴)/ApplicationHost(Dispatcher.Post)/create-release.ps1(공백) (2026-07-10)
