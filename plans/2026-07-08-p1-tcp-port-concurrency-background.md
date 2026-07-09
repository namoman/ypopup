# 계획: P1 + P3 + P2 — TCP 동시 접속 제한, 포트 변경 일관성, 백그라운드 실패 추적, XAML 경고, 크로스플랫폼 재검토

- 작성일: 2026-07-08
- 상태: 계획 — 승인 대기
- 관련 TODO:
  - **P1** — TCP 동시 접속 제한, 포트 변경 적용 일관성, 백그라운드 작업 실패 추적 (전부)
  - **P3** — Avalonia XAML 로더 경고 재발 여부 확인
  - **P2** — 크로스플랫폼 지원 범위 재검토
- 전제:
  - 모든 신규 코드는 모듈화 (한 파일에 몰지 않음)
  - 기존 시그니처 유지, 필요 시 오버로드로 확장
  - `CancellationToken` 전파 누락 없음
  - 코드 변경 후 `dotnet test` 39개 + 전체 빌드 유지

---

## 1. 검증된 코드 근거

### P1-1: TCP 동시 접속 제한

| 위치 | 코드 | 문제 |
|------|------|------|
| `TcpHostService.cs:41-42` | `_ = Task.Run(() => HandleClientAsync(...))` | 제한 없음 |
| `SharedFolderHostService.cs:101-102` | `_ = Task.Run(() => HandleClientAsync(...))` | 동일 |
| `YpopupCoordinator.cs:63-64` | `_ = RestartSharedFolderAsync()` | fire-and-forget |
| `TcpHostService.HandleClientAsync` line 99-103 | `catch (Exception ex) { Debug.WriteLine }` | 예외 무시 |

### P1-2: 포트 변경 적용 방식

| 위치 | 코드 | 문제 |
|------|------|------|
| `SettingsEditor.cs:153-155` | `requiresRestart = tcpPort != _originalSettings.TcpPort \|\| discoveryPort != _originalSettings.DiscoveryPort \| shareFolderPort != _originalSettings.ShareFolderPort` | 포트 변경 감지 |
| `SettingsEditor.cs:157` | `_coordinator.SaveSettings(_workingSettings)` | 저장 → 재시작 필요 메시지 |
| `YpopupCoordinator.cs:62-65` | 공유폴더 포트만 **즉시 재시작** | TCP/UDP 포트 변경 시 UI 메시지만, 실제 재시작 없음 |
| TcpHostService/DiscoveryService | **재시작 public 메소드 없음** | Coordinator에서 재시작 호출 불가 |

### P1-3: 백그라운드 작업 실패 추적

| 위치 | 코드 | 실패 |
|------|------|------|
| `YpopupCoordinator.cs:100-111` | `RestartSharedFolderAsync` catch | Debug.WriteLine |
| `YpopupCoordinator.cs:149-152` | HandleMessageReceivedAsync 자동답장 catch | Debug.WriteLine |
| `TcpHostService.cs:99-103` | HandleClientAsync catch | Debug.WriteLine |
| `SharedFolderHostService.cs:164-167` | HandleClientAsync catch | Debug.WriteLine |
| `DiscoveryService.cs:102-105` | AnnounceLoopAsync catch | Debug.WriteLine |

---

## 2. 범위

### P1-1: 들어오는 TCP 작업 수 제한

**SemaphoreSlim 기반 ConnectionLimiter** — 공용 클래스로 추출하여 TcpHost·SharedFolderHost 재사용.

| 신규 파일 | 내용 |
|-----------|------|
| `src/Ypopup.Core/Network/ConnectionLimiter.cs` | `SemaphoreSlim` 래퍼. `WaitAsync` + `Release` in try/finally. maxConcurrent=20 |

| 변경 파일 | 내용 |
|-----------|------|
| `TcpHostService.cs` | 필드 `_connectionLimiter` 추가, AcceptLoop에서 연결 수락 후 `_connectionLimiter.WaitAsync` 후 `HandleClientAsync` 실행. 초과는 await 대기. |
| `SharedFolderHostService.cs` | 동일 |
| `YpopupCoordinator.cs:46-51` | `StartAsync`에서 limiter 생성자 전달? or limiter를 내부 생성? |

**동작**:
- 연결 21번째 → `WaitAsync`에서 대기 (blocking 아님, await 대기)
- 1개 처리 완료 시 자동으로 대기 중인 연결 처리
- 거절이 아닌 **대기** 방식 → 사용자 불편 없음
- Default max = 20 (TCP 포트 스캔에도 버틸 수 있음)

**리팩토링**: `AcceptLoopAsync` 패턴이 TcpHostService와 SharedFolderHostService에 동일하므로 공통화 가능하나, **굳이 합치지 않음** (각각 Stop/Start 흐름이 다름). 각자 ConnectionLimiter만 주입.

### P1-2: 포트 변경 적용 방식

**접근**: 포트 변경 시 사용자에게 "재시작 필요" 메시지는 현재도 있음. 문제는 공유폴더만 자동 재시작되는 불일치. TCP/UDP 서비스도 재시작할 수 있게 만들면 일관성 회복.

| 변경 파일 | 내용 |
|-----------|------|
| `TcpHostService.cs` | `public async Task RestartAsync(CancellationToken)` 추가 — `StopAsync` 후 `StartAsync`. 또는 `StopAsync`+`StartAsync`를 public으로 열고 Coordinator에서 조합 |
| `DiscoveryService.cs` | 동일 `RestartAsync` 추가 |
| `YpopupCoordinator.cs:53-65` | `SaveSettings`에서 TCP/UDP 변경 감지 시 Discovery + TcpHost 재시작. **3개 서비스 (Discovery/TcpHost/SharedFolder) 동일 패턴으로 재시작** |

**세부**:
- TcpHostService: 현재 `StopAsync` 없음, `DisposeAsync`만 있음. `StopAsync` → `StartAsync` 사이클을 public으로 열어야 함.
- DiscoveryService: `DisposeAsync`만 있음. `StopAsync` public으로 열고 재시작 가능하게.
- 재시작 실패 시 `Debug.WriteLine` 대신 `BackgroundTaskTracker`(P1-3)로 처리.

**일관성 결과**:
| 포트 변경 | 적용 방식 |
|-----------|-----------|
| UDP/TCP 포트 | 저장 시 자동 재시작 (서비스 중단 1~2초) |
| 공유폴더 포트 | 저장 시 자동 재시작 (기존과 동일) |

### P1-3: 백그라운드 작업 실패 추적

**BackgroundTaskTracker** — fire-and-forget Task에 공통 예외 처리 + 상태 표시.

| 신규 파일 | 내용 |
|-----------|------|
| `src/Ypopup.Core/Helpers/BackgroundTaskTracker.cs` | `RunAsync(string operationName, Func<Task> task, Action<string, Exception>? onError = null)` — 예외 시 onError 콜백 + Debug.WriteLine |

**변경 파일**:
| 파일 | 변경 |
|------|------|
| `YpopupCoordinator.cs:63-64` | `_ = RestartSharedFolderAsync()` → `_ = BackgroundTaskTracker.RunAsync("공유폴더 재시작", () => RestartSharedFolderAsync(), OnBackgroundError)` |
| `YpopupCoordinator.cs:149-152` | 자동답장 catch 제거 → BackgroundTaskTracker 사용 |
| `TcpHostService.cs:41-42` | `_ = Task.Run(...)` 내부 catch 제거? No, HandleClientAsync 내부 catch는 그대로. AcceptLoop의 fire-and-forget 래핑. |
| `SharedFolderHostService.cs:101-102` | 동일 |

**onError 콜백**: Coordinator에서 `OnBackgroundError` 메소드 → Debug.WriteLine + 향후 UI 알림으로 확장 가능한 구조.

**기존 catch 블록과 관계**: HandleClientAsync 내부의 `catch (Exception ex) { Debug.WriteLine(...); CleanupPartialFiles(...); }`는 그대로 유지 (리소스 정리 필요). BackgroundTaskTracker는 AcceptLoop에서 Task.Run을 감싸는 레이어. HandleClientAsync가 throw하는 예외도 BackgroundTaskTracker에서 catch.

---

## 3. 모듈화된 파일 구조

### 신규 파일 (Core)
```
src/Ypopup.Core/
├── Network/
│   └── ConnectionLimiter.cs     # SemaphoreSlim 래퍼 (max 20, WaitAsync/Release)
└── Helpers/
    └── BackgroundTaskTracker.cs # fire-and-forget + 예외 처리 공통 헬퍼
```

### 변경 파일
```
src/Ypopup.Network/
├── Messaging/TcpHostService.cs      # ConnectionLimiter + RestartAsync
├── Sharing/SharedFolderHostService.cs  # ConnectionLimiter + StopAsync public
├── Discovery/DiscoveryService.cs     # StopAsync/RestartAsync public
└── YpopupCoordinator.cs              # SaveSettings 3개 서비스 일관 재시작 + BackgroundTaskTracker
```

테스트 추가:
```
tests/Ypopup.Core.Tests/
└── Network/
    ├── ConnectionLimiterTests.cs      # 동시 접속 제한, 취소
    └── BackgroundTaskTrackerTests.cs  # 예외 로깅, 정상 완료
```

---

## 2-B. P3 — Avalonia XAML 로더 경고 재발 여부 확인

**현재 상태**: `dotnet build` 시 5개 창에서 AVLN3001 경고 재발 중

| 창 | 코드 위치 | 현재 생성자 |
|----|-----------|-------------|
| ComposeWindow | `Views/Compose/ComposeWindow.axaml.cs:18` | `public ComposeWindow(YpopupCoordinator, PeerInfo)` |
| ReceiveWindow | `Views/Receive/ReceiveWindow.axaml.cs:18` | `public ReceiveWindow(YpopupCoordinator, ReceivedMessage)` |
| SettingsWindow | `Views/Settings/SettingsWindow.axaml.cs:11` | `public SettingsWindow(YpopupCoordinator)` |
| SharedFolderWindow | `Views/SharedFolder/SharedFolderWindow.axaml.cs:18` | `public SharedFolderWindow(YpopupCoordinator, PeerInfo)` |
| UserListWindow | `Views/UserList/UserListWindow.axaml.cs:17` | `public UserListWindow(YpopupCoordinator)` |

**경고 메시지**: `AVLN3001: XAML resource "avares://..." won't be reachable via runtime loader, as no public constructor was found`

### 원인

- Avalonia XAML 컴파일러가 창 초기화를 위해 **기본(public parameterless) 생성자**를 찾음
- 의존성 주입을 위해 매개변수 생성자만 정의된 창은 디자이너/런타임 로더가 인스턴스 생성 불가
- 경고 자체는 디자이너 문제일 뿐 런타임 동작에는 영향 없음 (`WindowNavigator.cs:39` 등에서 매개변수 생성자로 명시적 `new` 호출)

### 권장 결정: **경고 무시(A) 유지**

이유:
1. 런타임 동작에 영향 없음 (DI 흐름에서 `new ComposeWindow(coordinator, recipient)` 직접 생성)
2. 기본 생성자 추가 → "반쯤 초기화된" 객체 가능 → Less is more 원칙 위반
3. `TODO.md`에 "dotnet build를 경고 없이 유지하고, 실제 디자이너/런타임 문제 생길 때만 기본 생성자 추가 검토" 명시 → 현재 런타임 문제 없음

### 단계 (검증 only — 코드 변경 없음)

- [ ] `dotnet build` 경고 개수 확인 (현재 5개 AVLN3001 + 14개 CA1416 등 기존)
- [ ] 런타임 동작 검증: 앱 실행 → 사용자 목록/설정/쪽지/수신/공유폴더 창 정상 표시
- [ ] 5개 모두 런타임 정상 → **A(무시) 방침 유지**
- [ ] 만약 런타임/디자이너 문제 발견 → D 방식(`InitializeComponent`를 public 매개변수 메소드로 분리) 적용
- [ ] 결과를 walkthrough.md에 기록

---

## 2-C. P2 — 크로스플랫폼 지원 범위 재검토

### 현재 코드 기반 매트릭스 (조사 완료)

| 기능 | Windows | macOS | Linux | 근거 | 분류 |
|------|---------|-------|-------|------|------|
| **트레이 아이콘** | 동작 | 동작(`.ico` 시각적 문제 가능) | 동작(백엔드 의존) | `TrayIconManager.cs:13`, 분기 없음 | 모든 OS |
| **시작프로그램 등록** | 동작(레지스트리) | 미구현(no-op) | 미구현(no-op) | `WindowsStartupService.cs:49` vs `NullStartupService.cs:9` | Windows만 |
| **방화벽 규칙 자동 추가** | 동작(netsh, UAC) | 미구현(안내만) | 미구현(안내만) | `WindowsFirewallService.cs:144` vs `StubFirewallService.cs:24` | Windows만 |
| **방화벽 설정 열기** | 동작(firewall.cpl) | 예외 발생 | 예외 발생 | `WindowsFirewallService.cs:96` vs `StubFirewallService.cs:30` | Windows만 |
| **알림음** | 동작(MessageBeep) | 폴백(Console.Beep, 미동작 가능) | 폴백(Console.Beep, 미동작 가능) | `NotificationSoundService.cs:29,43` | 모든 OS(폴백 취약) |
| **부재 자동 감지(idle)** | 동작(GetLastInputInfo) | 미구현(항상 false) | 미구현(항상 false) | `WindowsAwayIdleDetector.cs:24` vs `NullAwayIdleDetector.cs:5` | Windows만 |
| **파일/폴더 다이얼로그** | 동작 | 동작 | 동작 | `ComposeWindow.axaml.cs:66`, `SharedFolderWindow.axaml.cs:127`, Avalonia StorageProvider | 모든 OS |
| **시작실행 체크박스** | 표시 | 숨김 | 숨김 | `GeneralSettingsPanel.axaml.cs:32` | Windows만 표시 |
| **"탐색기" 버튼(폴더 열기)** | 동작(explorer.exe) | **실패(예외)** | **실패(예외)** | `GeneralSettingsPanel.axaml.cs:222`, 분기 없음 | **잠재적 문제** |
| **파일/폴더/링크 열기(UseShellExecute)** | 동작 | 동작(open) | 동작(xdg-open) | `ReceiveWindow.axaml.cs:71,85`, `AboutWindow.axaml.cs:28` | 모든 OS |
| **패키징(publish.ps1)** | win-x64 | osx-arm64, osx-x64 | (없음) | `publish.ps1:120-130` | Windows+macOS |
| **README 안내** | 상세 | 다운로드만 | 언급 없음 | `README.md:21-25,74`, `AppInfo.cs:16` "Linux에서 사용 가능" → 과대 안내 | 부분적 |

### 발견된 잠재적 문제 (수정 대상)

1. **`GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` 직접 호출, OS 분기 없음**
   - macOS/Linux에서 "탐색기" 버튼 클릭 시 예외 → try/catch로 경고만 표시
   - 수정: UseShellExecute=true로 통일 (`Process.Start(new ProcessStartInfo(path){UseShellExecute=true})`) — OS별 `open`/`xdg-open` 자동 매핑
2. **`NotificationSoundService.cs:43` — `Console.Beep` 폴백**
   - `OutputType=WinExe`라 콘솔 없는 GUI 앱에서 비-Windows 시 소리 미출력
   - 수정 후보: macOS `NSBeep` P/Invoke 또는 Avalonia 사운드 API (스코프 밖 — 문서화만)
3. **`AppInfo.cs:16` — About 텍스트 "Linux에서 사용 가능"**
   - Linux 빌드가 publish.ps1에 없음 → 과대 안내
   - 수정: "Windows·macOS"로 축소 또는 Linux 빌드 추가
4. **`TrayIconLoader.cs:10` — `.ico` 단일 로드**
   - macOS에서 템플릿/PNG 권장 → 시각적 문제 가능 (문서화만)

### 단계

- [ ] 위 매트릭스를 `docs/cross-platform-support.md` 신규 파일로 작성 (문서)
- [ ] **수정 1**: `GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` 직접 호출 → `UseShellExecute=true`로 통일 (10분)
- [ ] **수정 2**: `AppInfo.cs:16` — "Linux" 과대 안내 수정 (5분)
- [ ] **문서화**: README에 크로스플랫폼 매트릭스 링크 추가 + Linux 미지원 명시
- [ ] **스코프 밖 항목**(문서화만): 알림음 폴백, 트레이 아이콘 포맷, macOS/Linux 시작프로그램 등록
- [ ] `dotnet build` + `dotnet test` 확인

---

## 4. 단계별 구현

### Phase P1-1 (TCP 동시 접속 제한)
- [ ] `ConnectionLimiter` 작성: `SemaphoreSlim(20)`, `WaitAsync(CancellationToken)`, `Release()`, `Dispose()`
- [ ] `ConnectionLimiterTests`: max 초과 시 대기, 정상 처리
- [ ] `TcpHostService` — `_connectionLimiter` 필드, AcceptLoop에서 WaitAsync 후 HandleClientAsync
- [ ] `SharedFolderHostService` — 동일
- [ ] 소규모 통합: `dotnet test` 39+α 통과 확인

### Phase P1-2 (포트 변경 일관성)
- [ ] `TcpHostService.StopAsync()` public 추가 (`DisposeAsync`에서 재사용)
- [ ] `TcpHostService.RestartAsync(CancellationToken)` — Stop + Start
- [ ] `DiscoveryService.StopAsync()` public + `RestartAsync(CancellationToken)`
- [ ] `YpopupCoordinator.SaveSettings` — TCP/UDP 포트 변경 시 Discovery·TcpHost 재시작
- [ ] 최종 일관성: 세 서비스 모두 재시작 or none → 일부만 재시작하지 않음
- [ ] `dotnet test` 통과 확인

### Phase P1-3 (백그라운드 실패 추적)
- [ ] `BackgroundTaskTracker` 작성 — `RunAsync(operationName, taskFunc, onError?)`
- [ ] `BackgroundTaskTrackerTests` 작성
- [ ] `YpopupCoordinator` — RestartSharedFolderAsync + 자동답장 fire-and-forget에 적용
- [ ] `TcpHostService.AcceptLoopAsync` — Task.Run 래핑
- [ ] `SharedFolderHostService.AcceptLoopAsync` — 동일
- [ ] 전체 `dotnet build` + `dotnet test` 39+ 전체 녹색

### Phase P3 (XAML 로더 경고 재발 여부 확인 — 검증 only)
- [ ] `dotnet build` 경고 개수 확인 (현재 5개 AVLN3001)
- [ ] 런타임 5개 창 정상 동작 확인 (앱 실행 → 사용자 목록/설정/쪽지/수신/공유폴더)
- [ ] 정상 동작 → A(경고 무시) 방침 유지
- [ ] 만약 문제 발견 시 → D 방식(InitializeComponent 분리) 적용
- [ ] 결과를 walkthrough.md에 기록

### Phase P2-C (크로스플랫폼 지원 범위 재검토)
- [ ] `docs/cross-platform-support.md` 신규 작성 — 위 매트릭스 그대로 기록
- [ ] **수정 1**: `GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` 직접 호출 → `UseShellExecute=true`로 통일
- [ ] **수정 2**: `AppInfo.cs:16` — "Linux에서 사용 가능" 과대 안내 → "Windows·macOS"로 축소
- [ ] README에 크로스플랫폼 매트릭스 링크 + Linux 미지원 명시
- [ ] `dotnet build` + `dotnet test` 확인

---

## 5. 실행 체크리스트

- [ ] Phase P1-1: `ConnectionLimiter` + `TcpHostService`/`SharedFolderHostService` 적용
- [ ] Phase P1-1: 단위 테스트 (ConnectionLimiter)
- [ ] Phase P1-2: `TcpHostService`/`DiscoveryService` StopAsync·RestartAsync public
- [ ] Phase P1-2: `YpopupCoordinator.SaveSettings` 3개 서비스 일관 재시작
- [ ] Phase P1-3: `BackgroundTaskTracker` + fire-and-forget 전환
- [ ] Phase P1-3: 단위 테스트 (BackgroundTaskTracker)
- [ ] Phase P3: `dotnet build` 경고 개수 확인 + 5개 창 런타임 동작 검증
- [ ] Phase P2-C: `docs/cross-platform-support.md` 매트릭스 작성
- [ ] Phase P2-C: `GeneralSettingsPanel` explorer.exe → UseShellExecute 수정
- [ ] Phase P2-C: `AppInfo` Linux 과대 안내 수정
- [ ] Phase P2-C: README 매트릭스 링크 + Linux 미지원 명시
- [ ] 최종: `dotnet build` 오류 0, `dotnet test` 전 녹색

---

## 6. 위험·제약

| 위험 | 대응 |
|------|------|
| 포트 변경 시 서비스 재시작 중(1~2초) 메시지 유실 가능 | 재시작 사이에 0.5초 간격 추가; 수신 불가 메시지는 자연스러운 현상임 |
| SemaphoreSlim queue backlog | max=20이면 충분히 큼. 필요 시 max 늘림 |
| 기존 `_ = Task.Run(...)`를 바꾸면 예외 처리 방식 변경 | BackgroundTaskTracker도 내부 try/catch → Debug.WriteLine으로 동일한 동작 유지 |
| BackgroundTaskTracker가 모든 예외를 먹지 않아야 | `onError` 콜백에서 사용자 표시 없이 로그만; 필요 시 확장 가능한 구조 |

---

## 7. 진행 상태

| 단계 | 상태 | 비고 |
|------|------|------|
| 계획 수립 | 완료 | 2026-07-08 |
| 사용자 승인 | 대기 | |
| Phase P1-1 | 미시작 | TCP 동시 접속 제한 |
| Phase P1-2 | 미시작 | 포트 변경 일관성 |
| Phase P1-3 | 미시작 | 백그라운드 실패 추적 |
| Phase P3 | 미시작 | XAML 로더 경고 재발 여부 확인 (검증 only) |
| Phase P2-C | 미시작 | 크로스플랫폼 지원 범위 재검토 |
| 최종 검증 | 미시작 | |

---

## 8. 승인 후 진행 순서

1. **P1-1**: ConnectionLimiter → 테스트 → TcpHostService + SharedFolderHostService 적용
2. **P1-2**: StopAsync/RestartAsync public → Coordinator 일관 재시작
3. **P1-3**: BackgroundTaskTracker → Coordinator → AcceptLoop fire-and-forget 교체
4. **P3**: `dotnet build` 경고 개수 확인 + 5개 창 런타임 동작 검증 → A(무시) 또는 D(분리) 결정
5. **P2-C**: `docs/cross-platform-support.md` 매트릭스 작성 → `GeneralSettingsPanel` explorer.exe 수정 → `AppInfo` Linux 안내 수정 → README 갱신
6. 전체 `dotnet build` + `dotnet test` 확인
7. 계획 MD 갱신 + walkthrough.md 기록
