# Y-popup 종합 구현·수정·개선 계획 (마스터)

- 작성일: 2026-07-08
- 상태: 승인 대기
- 대상: 지금까지 대화에서 논의된 모든 작업
- 원칙:
  - 모든 신규 코드는 모듈화 (한 파일에 몰지 않음)
  - 기존 시그니처 유지, 필요 시 오버로드로 확장
  - `CancellationToken` 전파 누락 없음
  - 코드 변경 후 `dotnet test` 39개 + 전체 빌드 녹색 유지

---

## 0. 진행 상태 요약

| Phase | 항목 | 상태 |
|-------|------|------|
| ✅ P0-A | 핵심 프로토콜·경로·설정 단위 테스트 | 완료 (31개 녹색) |
| ✅ P0-B | 공유폴더 통합 테스트 | 완료 (8개 녹색) |
| ✅ P2-X | 파일 전송·다운로드 진행률 + 취소 | 완료 |
| ⏳ P1-1 | TCP 동시 접속 제한 (`ConnectionLimiter`) | 계획됨 |
| ⏳ P1-2 | 포트 변경 일관성 (`StopAsync`/`RestartAsync`) | 계획됨 |
| ⏳ P1-3 | 백그라운드 실패 추적 (`BackgroundTaskTracker`) | 계획됨 |
| ⏳ P3-X | Avalonia XAML 로더 경고 재발 여부 확인 (검증 only) | 계획됨 |
| ⏳ P2-C | 크로스플랫폼 지원 범위 재검토 | 계획됨 |

범례: ✅ 완료 / ⏳ 계획됨

---

## 1. 완료된 작업 요약 (참고)

### P0-A: 핵심 프로토콜·경로·설정 단위 테스트
- `tests/Ypopup.Core.Tests` 신규 (xUnit)
- `FileNameSanitizer` → `Ypopup.Core/IO` 추출
- `SettingsService` internal 생성자 + `InternalsVisibleTo`
- 31개 테스트: PacketCodec(8) + SharedFolderPathHelper(5) + FileNameSanitizer(6) + SettingsMigration(6) + FileSendProgress(3) + FileReceiveProgress(3)

### P0-B: 공유폴더 통합 테스트
- `tests/Ypopup.Network.Tests` 신규
- `SharedFolderHostService` + `SharedFolderClient` 실제 TCP 통신 검증
- 8개 테스트: list root/sub/empty, non-existent, path traversal, download, etc.

### P2-X: 파일 전송·다운로드 진행률 + 취소
- `TransferProgress` 레코드 + `ProgressThresholdReporter` (1MB/5% 임계값)
- `PacketCodec`·`TcpHostService`·`SharedFolderClient`에 `IProgress` 오버로드
- `TransferProgressBar` UserControl (`Desktop/Controls`)
- `ComposeWindow` 송신 + `SharedFolderWindow` 다운로드 진행률 바 + 취소 버튼

---

## 2. 진행 예정 작업 상세

### Phase P1-1: TCP 동시 접속 제한 (구현)

**목적**: `TcpHostService`·`SharedFolderHostService`가 accept마다 무제한 Task.Run 하는 것을 SemaphoreSlim(20)으로 제한. 초과는 **대기**(거절 아님) → 사용자 불편 없음.

**신규 파일 (Core)**:
```
src/Ypopup.Core/Network/
└── ConnectionLimiter.cs   # SemaphoreSlim 래퍼 (max=20), WaitAsync/Release
```

**변경 파일 (Network)**:
- `TcpHostService.cs` — `_connectionLimiter` 필드, AcceptLoop에서 `WaitAsync` 후 `HandleClientAsync` 실행
- `SharedFolderHostService.cs` — 동일

**신규 테스트 (Core.Tests)**:
- `Network/ConnectionLimiterTests.cs` — max 초과 시 대기, 정상 처리, dispose

**검증**: `dotnet test` 39 + 신규 통과

---

### Phase P1-2: 포트 변경 적용 일관성 (구현)

**목적**: TCP/UDP 포트 변경 시 사용자 재시작 없이 **자동 재시작**. 공유폴더만 재시작되던 불일치 해결 → 3개 서비스(Discovery/TcpHost/SharedFolder) 일관적 재시작.

**변경 파일 (Network)**:
- `TcpHostService.cs` — `public async Task StopAsync()` + `public async Task RestartAsync(CancellationToken)` 추가. `DisposeAsync`에서 `StopAsync` 재사용.
- `DiscoveryService.cs` — 동일 (`StopAsync` + `RestartAsync`)
- `YpopupCoordinator.cs:53-65` — `SaveSettings`에서 TCP/UDP/공유폴더 포트 변경 감지 시 3개 서비스 일관 재시작

**검증**: `dotnet test` 39개 통과 + 포트 변경 후 즉시 적용 확인

---

### Phase P1-3: 백그라운드 작업 실패 추적 (구현)

**목적**: `_ = Task.Run(...)` 5곳의 fire-and-forget 작업을 공통 헬퍼로 모아 예외 로깅 + 확장 가능한 onError 콜백.

**신규 파일 (Core)**:
```
src/Ypopup.Core/Helpers/
└── BackgroundTaskTracker.cs   # RunAsync(operationName, taskFunc, onError?)
```

**변경 파일 (Network)**:
- `YpopupCoordinator.cs:63-64,149-152` — RestartSharedFolderAsync + 자동답장에 적용
- `TcpHostService.cs:41-42` — AcceptLoop의 Task.Run 래핑
- `SharedFolderHostService.cs:101-102` — 동일

**신규 테스트 (Core.Tests)**:
- `Network/BackgroundTaskTrackerTests.cs` — 정상 완료, 예외 로깅, 취소

**검증**: `dotnet test` 39 + 신규 통과

---

### Phase P3-X: Avalonia XAML 로더 경고 재발 여부 확인 (검증 only)

**목적**: 5개 창의 AVLN3001 경고("no public constructor")의 런타임 영향 검증.

**현재 경고 발생 창**:
| 창 | 위치 |
|----|------|
| ComposeWindow | `Views/Compose/ComposeWindow.axaml.cs:18` |
| ReceiveWindow | `Views/Receive/ReceiveWindow.axaml.cs:18` |
| SettingsWindow | `Views/Settings/SettingsWindow.axaml.cs:11` |
| SharedFolderWindow | `Views/SharedFolder/SharedFolderWindow.axaml.cs:18` |
| UserListWindow | `Views/UserList/UserListWindow.axaml.cs:17` |

**권장 결정**: **A(경고 무시) 유지**
- 런타임 동작에 영향 없음 (`WindowNavigator`에서 매개변수 생성자로 명시적 `new` 호출)
- 기본 생성자 추가 → "반쯤 초기화된" 객체 가능 → Less is more 위반
- TODO에 "실제 디자이너/런타임 문제 생길 때만 기본 생성자 추가 검토" 명시됨

**단계**:
- [ ] `dotnet build` 경고 개수 확인 (5개 AVLN3001)
- [ ] 앱 실행 → 사용자 목록/설정/쪽지/수신/공유폴더 5개 창 정상 동작 확인
- [ ] 정상 → A(무시) 방침 유지, walkthrough에 기록
- [ ] 문제 발견 시 → D 방식 (`InitializeComponent`를 public 매개변수 메소드로 분리)

---

### Phase P2-C: 크로스플랫폼 지원 범위 재검토 (문서 + 소수 수정)

**현재 매트릭스** (조사 완료):

| 기능 | Windows | macOS | Linux | 분류 |
|------|---------|-------|-------|------|
| 트레이 아이콘 | 동작 | 동작(`.ico` 시각 이슈 가능) | 동작 | 모든 OS |
| 시작프로그램 등록 | 동작(레지스트리) | 미구현 | 미구현 | Windows만 |
| 방화벽 자동 추가 | 동작(netsh) | 미구현 | 미구현 | Windows만 |
| 알림음 | 동작(MessageBeep) | 폴백(`Console.Beep`, 미동작 가능) | 동일 | 모든 OS(취약) |
| 부재 자동 감지 | 동작(GetLastInputInfo) | 미구현 | 미구현 | Windows만 |
| 파일 다이얼로그 | 동작 | 동작 | 동작 | 모든 OS |
| 시작실행 체크박스 | 표시 | 숨김 | 숨김 | Windows만 |
| **"탐색기" 버튼** | 동작(explorer.exe) | **실패(예외)** | **실패(예외)** | **잠재적 문제** |
| 파일/링크 열기(UseShellExecute) | 동작 | 동작 | 동작 | 모든 OS |
| 패키징 | win-x64 | osx-arm64/x64 | 없음 | Windows+macOS |
| README | 상세 | 다운로드만 | 언급 없음 | 부분적 |

**잠재적 문제 (수정 대상)**:
1. `GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` 직접 호출, OS 분기 없음 → `UseShellExecute=true`로 통일
2. `AppInfo.cs:16` — "Linux에서 사용 가능" 과대 안내 → "Windows·macOS"로 축소

**신규 파일 (docs)**:
```
docs/
└── cross-platform-support.md   # 위 매트릭스 + macOS/Linux 미지원 기능 안내
```

**변경 파일**:
- `src/Ypopup.Desktop/Views/Settings/Panels/GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` → `UseShellExecute=true`
- `src/Ypopup.Desktop/.../AppInfo.cs:16` — "Linux" 제거
- `README.md` — 크로스플랫폼 매트릭스 링크 + Linux 미지원 명시

**스코프 밖 (문서화만)**: 알림음 폴백, 트레이 아이콘 포맷, macOS/Linux 시작프로그램 등록

> **공유폴더 쓰기 기능은 제외** — 파일 공유 권한/충돌 문제로 복잡도가 크므로 읽기 전용 유지.

---

## 3. 신규 파일 전체 구조 (예정)

```
src/Ypopup.Core/
├── Network/
│   └── ConnectionLimiter.cs              # P1-1: SemaphoreSlim 래퍼
└── Helpers/
    └── BackgroundTaskTracker.cs          # P1-3: fire-and-forget 예외 처리

docs/
└── cross-platform-support.md            # P2-C: 크로스플랫폼 매트릭스

tests/Ypopup.Core.Tests/
└── Network/
    ├── ConnectionLimiterTests.cs         # P1-1
    └── BackgroundTaskTrackerTests.cs     # P1-3
```

## 4. 변경 파일 전체 (예정)

| Phase | 파일 | 변경 요약 |
|-------|------|-----------|
| P1-1 | `TcpHostService.cs` | `_connectionLimiter` + AcceptLoop에서 WaitAsync |
| P1-1 | `SharedFolderHostService.cs` | 동일 |
| P1-2 | `TcpHostService.cs` | `StopAsync`/`RestartAsync` public |
| P1-2 | `DiscoveryService.cs` | 동일 |
| P1-2 | `YpopupCoordinator.cs` | `SaveSettings`에서 3개 서비스 일관 재시작 |
| P1-3 | `YpopupCoordinator.cs` | fire-and-forget → `BackgroundTaskTracker.RunAsync` |
| P1-3 | `TcpHostService.cs` | AcceptLoop Task.Run 래핑 |
| P1-3 | `SharedFolderHostService.cs` | 동일 |
| P2-C | `GeneralSettingsPanel.axaml.cs:222` | `explorer.exe` → `UseShellExecute=true` |
| P2-C | `AppInfo.cs:16` | "Linux" 과대 안내 제거 |
| P2-C | `README.md` | 크로스플랫폼 매트릭스 링크 + Linux 미지원 명시 |

---

## 5. 실행 체크리스트

### Phase P1-1 (TCP 동시 접속 제한)
- [ ] `ConnectionLimiter` 작성 (SemaphoreSlim max=20)
- [ ] `ConnectionLimiterTests` 작성
- [ ] `TcpHostService`에 적용
- [ ] `SharedFolderHostService`에 적용
- [ ] `dotnet build` + `dotnet test` 통과

### Phase P1-2 (포트 변경 일관성)
- [ ] `TcpHostService.StopAsync()`/`RestartAsync()` public 추가
- [ ] `DiscoveryService.StopAsync()`/`RestartAsync()` public 추가
- [ ] `YpopupCoordinator.SaveSettings` 3개 서비스 일관 재시작 로직
- [ ] `dotnet build` + `dotnet test` 통과

### Phase P1-3 (백그라운드 실패 추적)
- [ ] `BackgroundTaskTracker` 작성
- [ ] `BackgroundTaskTrackerTests` 작성
- [ ] `YpopupCoordinator` — RestartSharedFolderAsync + 자동답장 적용
- [ ] `TcpHostService.AcceptLoopAsync` — Task.Run 래핑
- [ ] `SharedFolderHostService.AcceptLoopAsync` — 동일
- [ ] `dotnet build` + `dotnet test` 통과

### Phase P3-X (XAML 로더 경고 재발 여부 — 검증 only)
- [ ] `dotnet build` 경고 개수 확인 (현재 5개 AVLN3001)
- [ ] 앱 실행 → 5개 창 (사용자 목록/설정/쪽지/수신/공유폴더) 정상 동작 확인
- [ ] A(무시) 방침 유지 또는 문제 시 D(분리) 적용
- [ ] walkthrough에 결과 기록

### Phase P2-C (크로스플랫폼 재검토)
- [ ] `docs/cross-platform-support.md` 매트릭스 작성
- [ ] `GeneralSettingsPanel.axaml.cs:222` — `explorer.exe` → `UseShellExecute=true`
- [ ] `AppInfo.cs:16` — "Linux" 과대 안내 수정
- [ ] README에 매트릭스 링크 + Linux 미지원 명시
- [ ] `dotnet build` + `dotnet test` 통과

### 최종 검증
- [ ] `dotnet build` 오류 0
- [ ] `dotnet test` 전체 녹색 (39 + 신규)
- [ ] walkthrough.md 업데이트
- [ ] TODO.md 완료 항목 표시

---

## 6. 위험·제약

| 위험 | 대응 |
|------|------|
| 포트 변경 시 서비스 재시작 중(1~2초) 메시지 유실 가능 | 재시작 사이 0.5초 간격; 자연스러운 현상으로 안내 |
| SemaphoreSlim queue backlog | max=20이면 충분히 큼. 필요 시 max 증가 |
| 기존 `Task.Run` 패턴 변경 시 예외 처리 방식 변화 | `BackgroundTaskTracker`도 내부 try/catch → Debug.WriteLine으로 동일 동작 유지 |
| XAML 경고 무시 → 미래 Avalonia 버전에서 호환성 문제 | 릴리스 시마다 빌드 경고 모니터링; 문제 생기면 그때 D 방식 적용 |
| macOS 알림음 미동작 (Console.Beep) | P2-C에서는 문서화만; 향후 NSBeep/AVAudioPlayer 검토 |
| 공유폴더 쓰기 → 보안 (LAN 누구나 업로드) | X-Popup과 동일 모델; 필요 시 화이트리스트 옵션 검토 |

---

## 7. 진행 순서 (승인 후)

1. **P1-1**: ConnectionLimiter → 테스트 → TcpHost + SharedFolder 적용
2. **P1-2**: StopAsync/RestartAsync public → Coordinator 일관 재시작
3. **P1-3**: BackgroundTaskTracker → Coordinator → AcceptLoop fire-and-forget 교체
4. **P3-X**: 빌드 경고 확인 + 5개 창 런타임 동작 검증 → A(무시) 또는 D(분리) 결정
5. **P2-C**: 매트릭스 문서 작성 → explorer.exe 수정 → AppInfo 수정 → README 갱신
6. 최종: `dotnet build` + `dotnet test` 전체 녹색
7. 계획 MD 갱신 + walkthrough.md + TODO.md 기록

---

## 8. 진행 상태

| Phase | 상태 | 비고 |
|-------|------|------|
| P0-A | ✅ 완료 | 31개 테스트 |
| P0-B | ✅ 완료 | 8개 통합 테스트 |
| P2-X | ✅ 완료 | 진행률·취소 구현 |
| P1-1 | ⏳ 계획 | TCP 동시 접속 제한 |
| P1-2 | ⏳ 계획 | 포트 변경 일관성 |
| P1-3 | ⏳ 계획 | 백그라운드 실패 추적 |
| P3-X | ⏳ 계획 | XAML 경고 (검증 only) |
| P2-C | ⏳ 계획 | 크로스플랫폼 재검토 |
| 최종 검증 | 미시작 | |

---

## 9. 관련 문서

- `plans/2026-07-08-p0-tests-and-transfer-progress.md` — 완료된 P0·P2 진행률 상세
- `plans/2026-07-08-p1-tcp-port-concurrency-background.md` — P1·P3·P2-C 상세 (본 문서의 출처)
- `TODO.md` — 전체 TODO 목록
- `walkthrough.md` — 작업 기록