# Ypopup Walkthrough

## 2026-06-29 — X-Popup 클론 초기 구현

### 배경

- X-Popup(빨간전화기)은 Windows 11에서 NetBIOS/레거시 방식 때문에 동작이 불안정함
- 사무실·가정 LAN에서 설치 없이 쪽지/파일을 주고받을 현대적 대체 프로그램 필요

### 구현方針 (Less is more)

- 별도 서버 없이 **P2P** 구조 유지 (X-Popup과 동일 UX)
- 레거시 NetBIOS 대신 **UDP 브로드캐스트 + TCP** 사용
- **C# WPF** 단일 exe 배포 가능 구조
- MVP 범위: 사용자 탐색, 1:1 메시지, 파일 전송, 트레이, 수신 팝업

### 프로젝트 구조

```
Ypopup/
├── Ypopup.sln
└── src/Ypopup/
    ├── Models/          — AppSettings, PeerInfo, LanPacket
    ├── Protocol/        — JSON length-prefix 패킷 직렬화
    ├── Services/        — Discovery, TcpHost, Coordinator
    ├── Views/           — UserList, Compose, Receive, Settings
    └── Helpers/         — 트레이 아이콘 생성
```

### 핵심 기술

| 항목 | 포트/방식 |
|------|-----------|
| 사용자 탐색 | UDP 50505, 3초마다 Announce 브로드캐스트 |
| 메시지/파일 | TCP 50506, length-prefix JSON + 파일 스트림 |
| 피어 만료 | 15초 미응답 시 목록에서 제거 |

### X-Popup 대비 구현된 기능

- [x] 트레이 아이콘 (빨간 전화기 스타일)
- [x] 사용자 목록 자동 표시
- [x] 쪽지 송수신 팝업
- [x] 파일 첨부 / 다중 파일 / 드래그 앤 드롭
- [x] 수신 알림음
- [x] 환경 설정 (이름, 수신 폴더, 포트)
- [ ] 화면 캡처 전송 (향후)
- [ ] 원격 IP 직접 접속 (향후)
- [ ] 메시지 암호화 (향후)

### Windows 11 주의사항

- 방화벽에서 UDP 50505, TCP 50506 허용 필요
- 네트워크 프로필 **개인** 권장
- 포트 변경 시 프로그램 재시작 필요

## 2026-06-29 — 프로젝트 경로 및 저장소 이전

- 작업 경로: `D:\sw\dev\Ypopup`
- GitHub: https://github.com/namoman/ypopup
- 프로젝트명 `LanPopup` → `Ypopup`으로 통일 (네임스페이스, 설정 경로 포함)

## 2026-06-29 — Y-popup 명칭 통일 및 모듈 분리

- 프로그램 표시명: **Y-popup** (exe: `Y-popup.exe`)
- 솔루션 3개 프로젝트로 기능 분리
  - `Ypopup.Core` — Models, Protocol, Settings
  - `Ypopup.Network` — Discovery, Messaging, Coordinator
  - `Ypopup.App` — WPF UI
- `PublishSingleFile=true`로 **단일 exe 배포** 지원 (`publish.ps1`)

## 2026-06-29 — 완전 독립 exe (SelfContained)

- `SelfContained=true`로 .NET 런타임 포함 배포
- `EnableCompressionInSingleFile=true`로 exe 용량 압축
- 실행 PC에 .NET 8 Desktop Runtime 설치 불필요 (win-x64)

## 2026-06-29 — 설정 UI 프로토콜 정렬

- X-Popup 3탭 복제 → Y-popup 프로토콜 기준 4탭
  - **프로필**: UDP Announce (이름, 그룹, 메모, 이메일)
  - **네트워크**: IP, UDP/TCP 포트, 그룹 필터
  - **일반**: 알림, 수신 폴더, 글꼴, 창 동작
  - **부재**: 유휴 시간 + TCP 자동답장 메시지
- 마우스 위치 부재 (X-Popup 전용) 제거

## 2026-06-29 — 트레이·exe 아이콘

- `ref/icon.png` → `Assets/icon.png` (트레이) + `Assets/app.ico` (exe)
- `tools/generate-app-icon.ps1`로 PNG에서 `.ico` 생성
- `Ypopup.App.csproj`에 `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` 설정
- `publish.ps1` 실행 시 아이콘 자동 재생성 후 publish
- 트레이 아이콘은 32×32로 리사이즈 후 `System.Drawing.Icon` 변환

## 2026-06-29 — 트레이 아이콘 선명도 개선

### 원인

- PNG → `Bitmap.GetHicon()` 런타임 변환은 색·해상도 손실이 큼
- Windows 트레이는 주로 **16×16** 사용, 큰 PNG를 한 번만 32px로 줄이면 더 뭉개짐
- 원본 여백이 많으면 실제 아이콘이 작게 표시됨

### 수정

- `tools/generate-app-icon.ps1`: 투명 여백 자동 크롭 + **16/24/32/48** 다중 해상도 `.ico` 생성
- `Assets/tray.ico`를 WPF 리소스로 포함, `IconFactory`에서 `.ico` 직접 로드 (`GetHicon` 제거)
- exe용 `app.ico`도 동일 파이프라인으로 256px 포함 생성

## 2026-06-29 — 코드 리뷰 버그 수정

- **DiscoveryService**: `PruneAndNotify` lock 범위 수정 (스레드 안전성)
- **App 종료**: `DisposeResourcesAsync`로 이중 dispose 방지
- **IPv4 정규화**: `NetworkAddressHelper` — IPv6 매핑 주소(`::ffff:x.x.x.x`) 처리
- **TCP 연결**: 10초 타임아웃 추가
- **파일 수신**: `.partial` 임시 파일 사용, 실패 시 자동 삭제
- **Dispose**: Coordinator/Discovery/TcpHost idempotent 처리
- **UI**: 오류 메시지 프로그램명 `Y-popup` 통일

## 2026-06-29 — exe 즉시 종료 버그 수정

### 원인

- `TaskbarIcon.IconSource`에 PNG 리소스를 직접 지정하면 Windows 트레이용 `Icon` 변환에 실패
- 예외: `Argument 'picture' must be a picture that can be used as a Icon.`
- 예외가 `Application_Startup`에서 처리되지 않아 프로세스가 바로 종료됨

### 수정 (Less is more)

- `IconFactory.CreateTrayIcon()`에서 PNG → `System.Drawing.Bitmap` → `Icon`으로 변환
- `TaskbarIcon.Icon` 속성 사용 (`IconSource` 제거)
- 변환 실패 시 단색 fallback 아이콘 제공
- 디버그 계측(`AgentDebugLog`) 제거 후 `publish\Y-popup.exe` 재빌드

## 2026-06-29 — 정보 창 즉시 닫힘 수정

### 원인

- 트레이 컨텍스트 메뉴에서 `MessageBox.Show` 호출 시, 메뉴가 닫히면서 부모 창 없는 대화상자도 함께 사라짐 (WPF 트레이 앱 흔한 현상)

### 수정

- `AboutWindow` + `ShowDialog()`로 교체 (설정 창과 동일 패턴)
## 2026-06-29 — 네트워크 탭 방화벽 UI

- 설정 **네트워크** 탭에 방화벽 상태·허용 버튼 추가
- `FirewallHelper`로 netsh 규칙 등록 (관리자 UAC)

## 2026-07-03 — 홈페이지 앱 UI HTML/CSS mockup

- `docs/index.html` hero 영역: 정적 `screenshot.png` 대신 **순수 HTML/CSS**로 사용자 목록·쪽지 보내기 창 mockup
- Win11 바탕화면 프레임(글라스 작업 표시줄)은 유지, 실제 WPF 앱 라이트 테마 색상·레이아웃 반영
- 사용자 4명(홍길동·김영희·이철수·박민수), 선택 강조, 공유폴더 아이콘, 쪽지 작성 창 겹침 표시

## 2026-07-03 — GitHub Pages 다운로드·브랜딩 정리

- hero 제목을 **Y-popup(파란전화기)** 로 통일, 마퀴 문구도 파란전화기 반영
- Windows 32-bit 행·관련 각주 제거 (64비트만 배포)
- Windows .NET 8 Runtime 열: `Y-popup-win-x64-net8.zip` → `Y-popup-net8.exe` (framework-dependent 단일 exe, `publish.ps1`에서 이미 생성)

## 2026-07-03 — 설정 창 미표시·Avalonia 스타일 회귀 수정

### 배경

- Avalonia(`Ypopup.Desktop`) 전환 후 설정 창이 열리지 않는다는 제보
- UI 스타일이 WPF 라이트 테마 수정 이전 상태로 보인다는 제보
- `D:\sw\dev\lanpopup` 폴더는 없음 — `LanPopup`은 2026-06-29에 `Ypopup`으로 통일됨 (`walkthrough.md` 참고)

### 설정 창 원인

- 기본값 `KeepWindowTopmost = true`로 사용자 목록 창이 항상 위에 표시됨
- Avalonia에서 `ShowDialog(topmostOwner)` 호출 시 모달 설정 창이 **Topmost 부모 뒤에 가려짐** → 사용자 입장에선 "안 열림"
- WPF에서는 동일 Owner로 `ShowDialog()`해도 모달이 앞에 뜨지만, Avalonia는 Z-order 처리가 달라 회귀로 보임
- 트레이 `NativeMenu` 클릭은 UI 스레드 보장이 약해 `Dispatcher.UIThread.Post`로 설정·정보 열기 호출 정리

### 설정 창 수정 (Less is more)

- `WindowDialogHelper.ShowDialogAsync`: 표시 전 부모 `Topmost` 잠시 해제, 대화상자 `Topmost=true` + `CenterOwner`
- `WindowNavigator.ShowSettingsAsync` / `ShowAboutAsync`, `UserListWindow` 설정 버튼에 적용
- 트레이 메뉴 설정·정보 핸들러를 `Dispatcher.UIThread.Post`로 감쌈

### CSS(스타일) 회귀 원인

- WPF `App.xaml`에는 Window·TextBox·ListBox·TabItem·ComboBox·CheckBox 전역 스타일이 있음
- Avalonia `AppStyles.axaml`에는 **Button만** 이식되어 설정 창 등이 Fluent 기본 테마로 보임

### CSS 수정

- WPF `App.xaml` 라이트 테마 색상을 Avalonia `AppStyles.axaml`에 맞게 복원
- `Ypopup.Desktop.csproj`에 `Styles\**`를 `AvaloniaResource`로 명시 포함

## 2026-07-03 — 버전 2.0 릴리스

- `AppInfo.Version` **1.2** → **2.0** (UI 표시: 설정·정보 창)
- `Ypopup.Desktop.csproj` / `Ypopup.App.csproj`: `Version` **2.0.0**, `AssemblyVersion`·`FileVersion` **2.0.0.0**
- `app.manifest` assemblyIdentity **2.0.0.0**
- `publish.ps1`·`docs/index.html`·`README.md`에는 버전 문자열 없음 (csproj·AppInfo에서 빌드 시 반영)
- `publish.ps1` 실행 후 `docs/`·`publish*/` 바이너리를 GitHub Pages·저장소에 반영 (설정 창·스타일 수정 포함 2.0 빌드)
