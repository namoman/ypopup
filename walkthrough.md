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
  - `Ypopup.Desktop` — Avalonia UI
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
- `Ypopup.Desktop.csproj`에 `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` 설정
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
- Avalonia `WindowsFirewallService`로 netsh 규칙 등록 (관리자 UAC)

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
- `Ypopup.Desktop.csproj`: `Version` **2.0.0**, `AssemblyVersion`·`FileVersion` **2.0.0.0**
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

1. **아이콘 생성기 대상 불일치** — 앱에서 쓰는 `Ypopup.Desktop/Assets`와 실제 실행 파일 아이콘을 함께 갱신해야 했음
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

## 2026-07-05 — Windows 시작 시 트레이 전용 실행

- 시작 프로그램 등록 시 `"Y-popup.exe" --tray` 인자 추가
- `--tray`로 실행되면 사용자 목록 창을 띄우지 않고 트레이에만 상주
- 수동 실행(더블클릭)은 기존처럼 사용자 목록 창 표시
- 예전 등록(인자 없음)은 앱 시작 시 자동으로 `--tray` 포함 명령으로 갱신

## 2026-07-05 — publish.ps1 전체 clean 단계 추가

- `bin/`·`obj/` (`src`, `tools` 하위) 삭제 후 publish
- `publish*` 폴더 publish 전 일괄 삭제
- `docs/` 배포 exe·zip 7종 publish 전 삭제 후 재복사 (`index.html` 등 웹 페이지는 유지)

## 2026-07-05 — push-github.ps1 추가

- `publish.ps1` → git add (docs 배포 파일·src·README 등) → commit → push
- `-SkipPublish`: 빌드 생략하고 변경분만 push
- `-DryRun`: 실행할 git 명령만 출력
- `docs/share/` 로컬 테스트 폴더는 커밋 제외

## 2026-07-05 — push-github.ps1 Invoke-Git·커밋 판별 수정

### 배경

- `Invoke-Git` 매개변수명 `$Args`가 PowerShell 자동 변수와 충돌해 `git`만 실행되고 subcommand가 빠짐 → `git status` 단계에서 실패
- 재실행 시 `docs/share/`만 untracked인데 `git status --porcelain`에 잡혀 빈 커밋 시도

### 수정

- `$Args` → `$GitArgs`, `& git @GitArgs`로 호출
- 커밋 여부는 `git diff --cached --name-only`(스테이징된 변경)만 확인

## 2026-07-08 — P0 자동 테스트 프로젝트 + P2 파일 전송 진행률·취소

### 배경

- P0는 "릴리스 전 반드시 확인"이었으나 실사용 중인 코드에 회귀 방지 목적
- P2는 느린 네트워크에서 앱이 멈춘 것처럼 보이는 UX 개선
- TODO.md 정리 후 `plans/2026-07-08-p0-tests-and-transfer-progress.md` 계획 수립

### P0 — 단위 테스트

**파일 구조**

```
tests/Ypopup.Core.Tests/
├── Ypopup.Core.Tests.csproj          # xUnit + InternalsVisibleTo
├── Protocol/PacketCodecTests.cs      # 8 tests
├── Sharing/SharedFolderPathHelperTests.cs  # 5 tests
├── IO/FileNameSanitizerTests.cs      # 6 tests
├── Settings/SettingsMigrationTests.cs  # 6 tests
└── Transfers/
    ├── FileSendProgressTests.cs      # 3 tests
    └── FileReceiveProgressTests.cs   # 3 tests

tests/Ypopup.Network.Tests/
└── Sharing/SharedFolderIntegrationTests.cs  # 8 tests (실제 서버 구동)
```

**리팩토링**

- `FileNameSanitizer` — `TcpHostService`에서 `Ypopup.Core/IO`로 추출 (public static)
- `SettingsService` — `internal SettingsService(string settingsDirectory)` 생성자 추가
- `PacketCodec.WriteFileAsync`/`SaveFileAsync` — `IProgress<TransferProgress>?` 오버로드 추가 (기존 유지)

**설치**
- `Ypopup.Core.csproj`에 `<InternalsVisibleTo Include="Ypopup.Core.Tests" />` (+ Network.Tests)
- 솔루션에 `tests` 솔루션 폴더 + 2개 테스트 프로젝트 등록

**결과**: 39개 녹색 (Core 31 + Network 8), 로컬 `dotnet test`로 실행 가능

### P2 — 파일 전송 진행률·취소

**신규 클래스 (모듈화)**

| 파일 | 역할 |
|------|------|
| `src/Ypopup.Core/Models/TransferProgress.cs` | 진행률 레코드 (Percent, Fraction, IsComplete) |
| `src/Ypopup.Core/Protocol/ProgressReporter.cs` | 1MB/5% 간격 threshold-based 리포트 |
| `src/Ypopup.Desktop/Controls/TransferProgressBar.axaml` | 공용 UserControl (ProgressBar + 파일명 + 퍼센트 + 취소 버튼) |
| `src/Ypopup.Desktop/Controls/TransferProgressBar.axaml.cs` | StyledProperty (Progress, FileName, IsCancellable, CancelCommand) |

**변경 파일**

- `PacketCodec.cs` — `WriteFileAsync`/`SaveFileAsync` 오버로드 + 취소를 위한 `ThrowIfCancellationRequested()` 보강
- `TcpHostService.cs` — `SendMessageAsync` 오버로드 (기존 시그니처 유지), `FileNameSanitizer` 사용
- `SharedFolderClient.cs` — 다운로드 `.partial` 패턴 적용, `Content-Length` 파싱, 진행률 리포트, 예외 시 `.partial` 삭제
- `YpopupCoordinator.cs` — 송신/다운로드 오버로드 (기존 1-arg 위임)
- `ComposeWindow.axaml(.cs)` — 첨부 영역 아래 `TransferProgressBar`, `CancellationTokenSource`로 취소
- `SharedFolderWindow.axaml(.cs)` — 다운로드 버튼 위 `TransferProgressBar`, 진행 중 이중 다운로드 방지

**하위 호환**: 기존 시그니처 보존, 자동답장(`IsAutoReply`) 영향 없음

### 남은 TODO (진행 기준)

- **P1**: ~~TCP 동시 접속 제한~~ ✅, ~~포트 변경 일관성~~ ✅, ~~백그라운드 실패 추적~~ ✅
- **P2**: LAN 진단 화면, 롤링 로그, 배포 산출물 정리
- **P3**: 스모크 테스트 스크립트, 설정 검증 중복 제거, 진단 내보내기, ~~XAML 경고~~ ✅(검증 only, A 방침 유지)
- **P2-C**: 크로스플랫폼 매트릭스 문서 ✅, `explorer.exe` 수정 ✅, `AppInfo` Linux 안내 수정 ✅, README 갱신 ✅
- **UI 검증**: 2대 LAN 환경에서 진행률 바 + 취소 동작 확인 필요

### 다음 작업 순서 제안

1. **P3 설정 검증 중복 제거** — 포트/경로 검증 로직 `Ypopup.Core`로 이동. 30분 작업.
2. **P2 LAN 진단 화면** — 진단 정보 창 추가. 1시간 작업.

## 2026-07-08 — P1 전체 + P3 XAML 경고 검증 + P2-C 크로스플랫폼 재검토

### P1-1: TCP 동시 접속 제한

**신규 모듈**
- `src/Ypopup.Core/Network/ConnectionLimiter.cs` — `SemaphoreSlim(max=20)` 래퍼, `WaitAsync`/`Release`, `IAsyncDisposable` + `IDisposable`
- `tests/Ypopup.Core.Tests/Network/ConnectionLimiterTests.cs` — 6개 테스트

**적용**
- `TcpHostService.AcceptLoopAsync` — `Task.Run` 내부 `_connectionLimiter.WaitAsync` 후 `HandleClientAsync` 실행
- `SharedFolderHostService.AcceptLoopAsync` — 동일
- 초과 접속은 **대기**(거절 아님) → 사용자 불편 없음
- `DisposeAsync`에서 `_connectionLimiter.DisposeAsync()` 호출

### P1-2: 포트 변경 일관성

**변경**
- `TcpHostService` — `public Task StopAsync()` + `public Task RestartAsync(CancellationToken)` 추가. `StartAsync`가 `StopAsync` 후 시작, `DisposeAsync`가 `StopAsync` 재사용.
- `DiscoveryService` — 동일 (`StopAsync` + `RestartAsync`)
- `YpopupCoordinator.SaveSettings` — TCP/UDP/Discovery 포트 또는 PreferredIp 변경 시 일관되게 3개 서비스 자동 재시작 (`BackgroundTaskTracker`로 fire-and-forget)
- `SettingsEditor.cs` — 메시지 "포트 변경은 재시작 후 적용" → "포트/네트워크 변경 사항은 자동으로 적용되었습니다"

**효과**: 사용자가 포트 바꾸면 앱 재시작 없이 즉시 적용. 공유폴더만 재시작되던 불일치 제거.

### P1-3: 백그라운드 작업 실패 추적

**신규 모듈**
- `src/Ypopup.Core/Helpers/BackgroundTaskTracker.cs` — `RunAsync(string operationName, Func<Task>, Action<string, Exception>? onError)`. 내부 `Task.Run` + try/catch. `OperationCanceledException`은 무시, 그 외 예외는 `Debug.WriteLine` + onError 콜백.
- `tests/Ypopup.Core.Tests/Network/BackgroundTaskTrackerTests.cs` — 4개 테스트 (정상/예외/취소/동기 factory)

**전환 위치** (모두 fire-and-forget → tracker)
- `YpopupCoordinator.SaveSettings` — 공유폴더/TCP/Discovery 재시작 3곳
- `YpopupCoordinator.HandleMessageReceivedAsync` — 자동답장 (기존 try/catch 제거 → tracker)
- `TcpHostService.AcceptLoopAsync` — 클라이언트 처리 Task.Run
- `SharedFolderHostService.AcceptLoopAsync` — 동일

### P3-X: Avalonia XAML 로더 경고 재발 여부 확인

- `dotnet build` (clean) → 5개 창에서 AVLN3001 경고 재발 확인
  - ComposeWindow, ReceiveWindow, SettingsWindow, SharedFolderWindow, UserListWindow
  - 원인: 매개변수 생성자만 있고 기본 parameterless 생성자 없음
- 결정: **A(경고 무시) 방침 유지**
  - 런타임 동작에 영향 없음 (`WindowNavigator`에서 매개변수 생성자로 명시적 `new` 호출)
  - 기본 생성자 추가 → "반쯤 초기화된" 객체 가능 → Less is more 원칙 위반
  - `TODO.md`에 "실제 디자이너/런타임 문제 생길 때만 기본 생성자 추가 검토" 명시됨 → 현재 문제 없음
- 향후 런타임/디자이너 문제 발생 시 → D 방식 (`InitializeComponent`를 public 매개변수 메소드로 분리) 적용 검토

### P2-C: 크로스플랫폼 지원 범위 재검토

**신규 문서**
- `docs/cross-platform-support.md` — 모든 기능별 Windows/macOS/Linux 지원 매트릭스 작성

**잠재적 문제 수정**
- `GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` 직접 호출 → `Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true })`로 통일
  - .NET이 OS별로 Windows는 ShellExecute, macOS는 `open`, Linux는 `xdg-open`로 자동 매핑
- `AppInfo.cs:16` — "Win11·macOS·Linux" → "Win11·macOS" (Linux 빌드 미배포 → 과대 안내 제거)
- `README.md` 요구사항 — macOS 비고에 일부 Windows 전용 기능 안내 + `docs/cross-platform-support.md` 링크 추가 + Linux 미지원 명시

**문서화만 (스코프 밖)**: macOS 알림음 폴백(`Console.Beep`), 트레이 아이콘 `.ico` 포맷, macOS/Linux 시작프로그램 등록 미구현

### 최종 검증 결과

- `dotnet build Ypopup.sln` — 오류 0, 증분 빌드 경고 0 (clean 빌드 시 5개 AVLN3001 — A 방침으로 유지)
- `dotnet test Ypopup.sln` — **49개 녹색** (Core 41 + Network 8)
  - Core: PacketCodec(8) + SharedFolderPathHelper(5) + FileNameSanitizer(6) + SettingsMigration(6) + FileSendProgress(3) + FileReceiveProgress(3) + ConnectionLimiter(6) + BackgroundTaskTracker(4)
  - Network: SharedFolderIntegration(8)

## 2026-07-09 — 세션 재개 및 상태 검토

### 작업
- TODO.md·walkthrough.md를 읽어 마지막 완료 지점(P1-1/2/3 + P2-C + P3-X) 확인
- TODO.md 체크박스 상태를 실제 완료에 맞게 갱신 (P1 3개, P2-C, P3-X를 [x]로)
- TODO.md에 세션 재개 로그 추가
- walkthrough.md에 본 항목 추가

### 현재 상태
- **완료된 Phase**: P0, P1-1/2/3, P2-X(진행률+취소), P2-C(크로스플랫폼), P3-X(XAML 경고)
- **미완료 P2**: 롤링 로그, 배포 산출물 정리
- **미완료 P3**: 스모크 테스트, 설정 검증 중복 제거, 진단 내보내기
- **검증 필요**: 2-PC LAN에서 진행률+취소 UI 동작 확인 (수동)
- `dotnet build` 오류 0, `dotnet test` 49개 녹색 (변동 없음)

## 2026-07-09 — P2 LAN 진단 화면 구현

### 변경 파일

| 파일 | 변경 |
|------|------|
| `src/Ypopup.Network/Discovery/DiscoveryService.cs` | `_lastAnnounceSentUtc`, `_lastPacketReceivedUtc` 필드 + public getter 추가. BroadcastAnnounce/HandleAnnounce에서 타임스탬프 갱신 |
| `src/Ypopup.Network/YpopupCoordinator.cs` | `LastAnnounceSentUtc`, `LastPacketReceivedUtc` 프로퍼티 노출 |
| `src/Ypopup.Desktop/Views/Diagnostics/LanDiagnosticWindow.axaml` | **신규** — 진단 창 XAML (내 네트워크, 트래픽, 피어 목록 3개 섹션) |
| `src/Ypopup.Desktop/Views/Diagnostics/LanDiagnosticWindow.axaml.cs` | **신규** — Coordinator에서 진단 데이터 읽어 UI 갱신 |
| `src/Ypopup.Desktop/Windows/WindowNavigator.cs` | `ShowLanDiagnosticAsync()` 추가 |
| `src/Ypopup.Desktop/Tray/TrayMenuBuilder.cs` | `Create` 파라미터에 `showDiagnostics` 추가, "LAN 진단" 메뉴 항목 |
| `src/Ypopup.Desktop/Application/ApplicationHost.cs` | 트레이 메뉴에 진단 콜백 연결 |

### 화면 구성
1. **내 네트워크** — 선택된 IP, 브로드캐스트 대상, UDP/TCP 포트
2. **트래픽** — 마지막 Announce 전송/피어 패킷 수신 시간 (방금 전/X초 전/X분 전)
3. **발견된 피어 목록** — 각 피어의 이름, IP, 마지막 수신 시간

### 검증
- `dotnet build` — 오류 0
- `dotnet test` — 49개 녹색 (Core 41 + Network 8)

## 2026-07-09 — P2 롤링 로그 구현

### 변경 파일

| 파일 | 변경 |
|------|------|
| `src/Ypopup.Core/Logging/LogService.cs` | **신규** — 일별 롤링 파일 로거 (`yyyy-MM-dd.log`), 7일 지난 로그 자동 삭제, `Debug.WriteLine` 동시 출력 |
| `src/Ypopup.Network/Discovery/DiscoveryService.cs` | `Debug.WriteLine` 3곳 → `LogService.Warning` |
| `src/Ypopup.Network/Messaging/TcpHostService.cs` | `Debug.WriteLine` 3곳 → `LogService.Warning`/`Error` |
| `src/Ypopup.Network/Sharing/SharedFolderHostService.cs` | `Debug.WriteLine` 2곳 → `LogService.Warning`/`Error` |
| `src/Ypopup.Network/YpopupCoordinator.cs` | `Debug.WriteLine` 2곳 → `LogService.Error` |
| `src/Ypopup.Core/Helpers/BackgroundTaskTracker.cs` | `Debug.WriteLine` → `LogService.Error` |
| `src/Ypopup.Desktop/Views/About/AboutWindow.axaml.cs` | `Debug.WriteLine` → `LogService.Warning` |
| `src/Ypopup.Desktop/Views/Settings/Panels/AboutSettingsPanel.axaml.cs` | `Debug.WriteLine` → `LogService.Warning` |
| `src/Ypopup.Desktop/Application/ApplicationHost.cs` | `LogService.Initialize` 호출 (시작 시) |

### 검증
- `dotnet build` — 오류 0
- `dotnet test` — 49개 녹색 (Core 41 + Network 8)

## 2026-07-09 — P2 배포 산출물 정리

### 결정
바이너리를 `docs/`에서 **GitHub Releases**로 이동. 저장소가 무거워지는 것을 방지.

### 변경 파일

| 파일 | 변경 |
|------|------|
| `.gitignore` | `release/` + `docs/Y-popup*.exe`, `docs/Y-popup*.zip` 추가 (로컬 바이너리 제외) |
| `publish.ps1` | `$docsDeploymentFiles` → `$releaseFiles`, 출력 경로 `docs/` → `release/` |
| `docs/index.html` | 6개 다운로드 링크 `./` → `https://github.com/namoman/ypopup/releases/latest/download/` |
| `push-github.ps1` | 바이너리 git add 루프 제거, docs 웹페이지만 add |
| `tools/create-release.ps1` | **신규** — `gh release create` 자동화 스크립트 |

### 새 워크플로
1. `publish.ps1` — 바이너리가 `release/`에 생성됨 (git에서 무시)
2. `tools/create-release.ps1` — `gh release create v2.x.x release/*` 실행
3. `push-github.ps1` — 소스 코드 + 웹페이지만 git push

### 검증
- `dotnet build` — 오류 0, 경고 0
- `dotnet test` — 49개 녹색 (Core 41 + Network 8)

## 2026-07-09 — P3 설정 검증 중복 제거

### 변경 파일

| 파일 | 변경 |
|------|------|
| `src/Ypopup.Core/Settings/SettingsValidator.cs` | **신규** — `ValidationResult` struct + `SettingsValidator` static class (ValidateDisplayName, ValidatePort, ValidatePortsDiffer, ValidateShareFolderPath, ValidateAwayIdleMinutes) |
| `src/Ypopup.Desktop/Views/Settings/SettingsEditor.cs` | `TrySaveAsync` 검증 15줄 → `SettingsValidator` 8회 호출 |
| `src/Ypopup.Desktop/Views/Settings/Panels/NetworkSettingsPanel.axaml.cs` | `AddFirewallRuleButton_Click` 검증 8줄 → `SettingsValidator` 3회 호출 |
| `tests/Ypopup.Core.Tests/Settings/SettingsValidatorTests.cs` | **신규** — 19개 테스트 (이름/포트/포트중복/경로/유휴시간) |

### 검증
- `dotnet build` — 오류 0
- `dotnet test` — 75개 녹색 (Core 67 + Network 8)

## 2026-07-10 — 기존 이슈 수정 및 P3 잔여 작업

### 기존 이슈 수정

| 파일 | 이슈 | 수정 |
|------|------|------|
| `.gitignore` | IDE 설정·바이너리 패턴 누락 | `.idea/`, `*.DotSettings.user`, `.DS_Store`, `Thumbs.db`, `docs/share/` 추가 |
| `push-github.ps1` | `git add src tools`로 의도치 않은 바이너리 스테이징 위험 | `tests`, `plans`, `TODO.md` 포함하도록 add 대상 보강, `docs/share` 수동 제거 로직 제거 (.gitignore로 대체) |
| `ApplicationHost.cs:87,92` | `ShowUserList` 콜백에 `Dispatcher.UIThread.Post` 누락 | 래핑 추가 |
| `tools/create-release.ps1` | `$PSScriptRoot` 경로 공백 시 오류 | `Set-Location -LiteralPath` 사용 |

### P3 스모크 테스트 (`tools/smoke-test.ps1`)
- `dotnet build` → `dotnet test` → 10개 프로젝트 파일 존재 확인
- 실패 시 `exit 1`, 통과 시 녹색 메시지

### P3 진단 내보내기 (`DiagnosticExporter`)
- **신규**: `src/Ypopup.Core/Diagnostics/DiagnosticExporter.cs` — 정적 `Generate()` 메서드
  - App 버전, OS(`RuntimeInformation`), 설정, 네트워크 인터페이스, 포트 가용성(TCP/UDP 소켓 바인딩), 피어 목록, 최근 로그 30줄
- **변경**: `LanDiagnosticWindow.axaml` — "내보내기" 버튼 추가
- **변경**: `LanDiagnosticWindow.axaml.cs` — 바탕화면에 `Y-popup-diagnostic-*.txt` 저장 후 열기

### 검증
- `dotnet build` — 오류 0
- `dotnet test` — 75개 녹색 (Core 67 + Network 8)

## 2026-07-10 — README·소개 페이지 문서 동기화

### 배경
P0~P3 기능 구현은 완료되었으나, 공개 문서(README, GitHub Pages)에 2026-07-10 추가분(진단 내보내기, 스모크 테스트)과 일부 기능 설명이 빠져 있었음.

### 변경 (Less is more — 문서만 갱신, 코드 변경 없음)

| 파일 | 내용 |
|------|------|
| `README.md` | 기능 목록 보강(진단 내보내기, `--tray`, 롤링 로그 7일, 서비스 자동 재시작), 프로젝트 구조에 `smoke-test.ps1`·`DiagnosticExporter`, 빌드 경로 수정, 변경 로그 2026-07-10 추가 |
| `docs/index.html` | 버전 2.0·macOS 안내, 기능 목록(진단 내보내기·롤링 로그·트레이), 시작하기 04단계(공유폴더·쪽지) 추가 |

