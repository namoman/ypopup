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
- `publish.ps1` 실행 후 `docs/` 바이너리만 GitHub Pages·저장소에 반영 (`publish*` 중간 폴더는 제외)

## 2026-07-03 — 사용자 목록 정렬·설정 창 종료 수정

### 배경

- 사용자 목록 항목이 창 너비에 맞지 않고 배경이 비쳐 보임
- 설정(⚙) 클릭 시 프로그램이 종료됨

### 원인

- `UserListWindow`: `Background="Transparent"` + `ExtendClientAreaToDecorationsHint`로 데스크톱이 비침, 항목 `Width="290"` 고정으로 좌측 치우침
- `WindowDialogHelper`: 모달에 `Topmost=true`를 설정하면 Avalonia에서 `ShowDialog` 시 크래시 가능

### 수정 (Less is more)

- 사용자 목록 창 배경을 `#F5FFFFFF` 불투명으로, 항목은 `HorizontalAlignment="Stretch"` + `peer-list` 전용 ListBox 스타일
- `WindowDialogHelper`에서 대화상자 `Topmost` 제거 (부모 Topmost만 잠시 해제)
- 설정 버튼에 예외 처리 추가

## 2026-07-03 — 설정 NullReference·버튼 내용 정렬 수정

### 설정 창

- `InitializeComponent()` 중 `TabControl.SelectionChanged`가 `_editor` 할당 전에 발생 → `NullReferenceException`
- `_editor` nullable + SelectionChanged 초기 호출 무시로 수정

### 버튼 정렬

- WPF `App.xaml`은 Button `ControlTemplate`에서 `ContentPresenter`를 Center 정렬
- Avalonia `AppStyles.axaml`에 동일 템플릿·`Horizontal/VerticalContentAlignment` 적용
- 쪽지/사용자 목록 하단 버튼 영역 `VerticalAlignment="Center"` 보강
- 사용자 목록 창 배경은 투명(`Transparent`) 유지

## 2026-07-03 — publish 중간 폴더 Git 제외

- `publish-framework/`, `publish-osx-*` 등은 `publish.ps1` **로컬 중간 산출물** (재생성 가능)
- `.gitignore`에 추가하고 Git 추적 제거 — 배포 파일은 `docs/`만 유지

## 2026-07-03 — 설정 탭 제목 스타일

- WPF `App.xaml`의 `TabItem` 커스텀 템플릿(가운데 정렬·선택 시 빨간 밑줄)이 Avalonia에 없어 Fluent 기본 탭만 적용되던 문제
- `AppStyles.axaml`에 동일 `TabItem` 템플릿 이식 (hover/selected Foreground·BorderBrush)

## 2026-07-03 — 부재 탭 안내문 정리

- 기술 용어(`IsAway=true`, UDP/TCP) 안내문 → 사용자용 문구로 변경
- WIP에 있던 수정본 기준: 부재 표시 + 자동 답장을 일반어로 설명

## 2026-07-03 — WPF·Avalonia 설정 패리티 및 레거시 경로 마이그레이션

### 배경

- lanpopup에서 정리했던 수신/공유 기본 폴더(`exe\down`, `exe\share`) 등이 Avalonia 쪽에 누락·회귀된 항목이 계속 발견됨
- WPF를 기준으로 Core·UI를 맞춤 (Less is more: 공통 로직은 Core로, UI 차이만 최소 수정)

### Core (`SettingsService`, `SharedFolderPathHelper`)

- `GetDefaultReceiveDirectory()` 추가 — 수신 기본 경로 `exe\down` 단일 정의
- 로드 시 레거시 경로 자동 마이그레이션 후 `settings.json`에 즉시 저장:
  - 수신: `Documents\Y-popup\Received` → `exe\down`
  - 공유: `Documents\Y-popup\공유폴더`, `publish\share` → `exe\share`
- 저장 시에도 동일 정규화 적용

### Avalonia 설정 UI

- **네트워크**: 공개 IP `ComboBox` `IsEditable` + `Text` 기반 로드/저장 (WPF와 동일)
- **일반**: 수신 폴더 안내 문구 추가; Windows가 아니면 자동 실행 체크박스 숨김
- **프로필**: 메모 필드 스크롤 + 전체 패널 `ScrollViewer`
- **공유폴더 시작 실패**: 안내 문구를 WPF와 동일하게 (`설정 > 네트워크 > 방화벽`)

### 설정 저장 후 연동

- `YpopupCoordinator.SettingsSaved` 이벤트 추가
- 저장 후 부재 상태 갱신 (`AwayMonitorService.RefreshAwayStatus`)
- 사용자 목록 창 `Topmost` 설정 즉시 반영 (`RefreshPeers`)

### WPF

- 공개 IP `IsEditable`, 수신 폴더 안내 문구, `RefreshPeers` 시 `Topmost` 갱신 동기화

## 2026-07-03 — 아이콘 미반영 원인 수정 및 클린 빌드

### 왜 PC에서 아이콘이 안 바뀌어 보였나

1. **아이콘 생성기가 Desktop만 갱신** — `Ypopup.App/Assets`는 예전 `.ico`가 남아 있었음
2. **창 헤더 로고가 이모지(☎)** — `ref/icon.png`를 쓰지 않고 빨간 박스+전화 이모지가 하드코딩되어 있었음
3. **이전 exe 실행** — `dotnet run` 캐시·바탕화면 바로가기·Windows 아이콘 캐시로 예전 아이콘이 보일 수 있음

### 수정

- `IconGenerator`: `ref/icon.png` → **Desktop + App** 양쪽 `Assets/`에 `icon.png`, `tray.ico`, `app.ico` 동시 생성
- 사용자 목록 창 헤더: `icon.png` 이미지로 교체 (WPF·Avalonia)
- `bin/`·`obj/`·`publish*` 삭제 후 `publish.ps1`로 재빌드 → `docs/Y-popup.exe` 갱신

### 확인 방법

- 새 빌드 실행: `D:\sw\dev\Ypopup\docs\Y-popup.exe` 또는 `D:\sw\dev\Ypopup\publish\Y-popup.exe` (publish 폴더는 빌드 후 삭제됨)
- 트레이·작업 표시줄 아이콘이 여전히 예전이면: Y-popup 완전 종료 후 새 exe 실행 (Windows 탐색기 아이콘 캐시는 재부팅 또는 exe를 다른 폴더로 복사하면 갱신됨)

## 2026-07-03 — 메인 헤더·설정 탭 UI 조정

- **창 안 로고**: `icon.png` → 빨간 박스 + ☎ 이모지로 복원 (exe/트레이 아이콘만 `ref/icon.png` 사용)
- **메인 헤더 제목**: `Y-popup` → 현재 사용자 **표시 이름** (`Settings.DisplayName`, 설정 저장 후 갱신)
- **설정 탭**: Fluent 기본 큰 글씨 대신 12px·얇은 회색/선택 시 빨간 밑줄 스타일 (`HeaderTemplate` + `MinHeight=0`)

## 2026-07-03 — 사용자 목록 영역 배경 구분

- 목록 `ListBox`를 `#F1F5F9` 패널(둥근 모서리·연한 테두리)로 감싸 창 본문(`#F5FFFFFF`)과 시각적으로 구분
- 목록 항목 hover 시 `#E2E8F0` 배경 (Avalonia `peer-list` 스타일)

## 2026-07-03 — 공유폴더 상대방 미표시 원인·수정

### 흔한 원인

1. **파일 위치 오류** — 공유되는 폴더는 설정의 `ShareFolderPath`(기본: **실행 exe 옆 `share`**)입니다. 다른 경로에 넣으면 상대는 빈 목록만 봅니다.
2. **공유 서버 미실행** — 방화벽 TCP 50507 차단 시 서버가 안 떠도 예전에는 UDP로 “공유 있음”만 알려질 수 있었음.
3. **HTTP 클라이언트 호환** — 공유폴더는 경량 TCP HTTP 서버인데 `HttpClient`와 맞지 않을 수 있어 직접 TCP 요청으로 교체.

### 수정

- `SharedFolderHttpIO` + TCP 기반 목록/다운로드 클라이언트
- 서버: HTTP 헤더 전체 수신 후 응답
- UDP Announce: **공유폴더 서버가 실제 실행 중일 때만** `ShareFolderEnabled` 전송
- 앱 시작 순서: 공유폴더 호스트 → 탐색(Announce) → 메시지 TCP
- 설정 > 일반: 공유 폴더 경로·파일/하위폴더 개수 표시
- 상대가 빈 공유폴더를 열면 안내 메시지 표시
- 존재하지 않는 공유 경로는 로드 시 `exe\share`로 정규화
