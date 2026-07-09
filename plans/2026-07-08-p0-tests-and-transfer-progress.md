# 계획: P0 자동 테스트 + 파일 전송 진행률·취소

- 작성일: 2026-07-08
- 상태: 계획 승인 대기
- 관련 문서: `TODO.md` P0(테스트 프로젝트), P2(파일 전송 진행률·취소)
- 원침
  - 모든 소스는 한 파일에 몰아넣지 않고 모듈화
  - 프로덕션 코드 기존 시그니처는 하위 호환 유지 (신규 오버로드로 확장)
  - UI 스레드 안전: `Progress<T>`는 UI 스레드에서 생성
  - 취소는 `CancellationToken`으로만 전파, `OperationCanceledException` 명시적 처리

---

## 1. 배경·검증된 코드 근거

| 항목 | 코드 근거 | 비고 |
|------|-----------|------|
| 테스트 프로젝트 없음 | `Ypopup.sln:6-11` (Core/Network/Desktop 3개만) | P0 |
| `PacketCodec` 패킷 검증 | `src/Ypopup.Core/Protocol/PacketCodec.cs:46,99,116` | P0 |
| `SharedFolderPathHelper` 보안 | `src/Ypopup.Core/Sharing/SharedFolderPathHelper.cs:14-30` | P0 |
| `SettingsService` 경로 고정 | `src/Ypopup.Core/Settings/SettingsService.cs:16-20` | 테스트 격리 처리 필요 |
| `TcpHostService` 파일명 처리 | `src/Ypopup.Network/Messaging/TcpHostService.cs:189-219` (`private static`) | 별도 클래스로 추출 |
| 송신 `WriteFileAsync` | `src/Ypopup.Core/Protocol/PacketCodec.cs:56-72` | 진행률·취소 추가 |
| 수신 `SaveFileAsync` | `src/Ypopup.Core/Protocol/PacketCodec.cs:74-105` | 진행률·취소 추가 (수신 UI는 표시 없음, 단위 테스트만) |
| `SendMessageAsync` 호출자 | `src/Ypopup.Network/Messaging/TcpHostService.cs:110-159` + `YpopupCoordinator.SendMessageAsync:85-89` | 신규 오버로드 |
| `DownloadBinaryAsync` | `src/Ypopup.Network/Sharing/SharedFolderClient.cs:63-139` | Content-Length 파싱·진행률·`.partial` 추가 |
| Compose UI | `src/Ypopup.Desktop/Views/Compose/ComposeWindow.axaml:54-90` + `.cs:138-179` | 진행률 영역·취소 버튼 추가 |
| SharedFolder UI | `src/Ypopup.Desktop/Views/SharedFolder/SharedFolderWindow.axaml:37-41` + `.cs:107-141` | 진행률 영역·취소 버튼 추가 |

### 검증에서 발견한 결정 사항 (사용자 승인 완료)
1. `SettingsService` → `internal` 생성자 + `InternalsVisibleTo("Ypopup.Core.Tests")`로 테스트 격리
2. `SanitizeFileName`/`GetUniquePath` → `Ypopup.Core/IO/FileNameSanitizer.cs`로 별도 추출
3. TCP 수신측(Receive) 진행률 UI → 미포함 (송신 측 ProgressBar로 충분)

---

## 2. 범위

### A. P0 테스트 프로젝트 (자동 테스트)
- 新 `tests/Ypopup.Core.Tests` xUnit 프로젝트
- 단위 테스트만 (네트워크 불필요, 로컬 `dotnet test` 실행 가능)

### B. 파일 전송 진행률·취소 (UI)
- 송신: `ComposeWindow`에 `/api` 진행률 ProgressBar + 취소 버튼
- 다운로드: `SharedFolderWindow`에 진행률 ProgressBar + 취소 버튼
- 수신: 진행률 UI 표시 없음 (단위 테스트만)

---

## 3. 모듈화된 파일 구조 (신규·변경)

### 신규 파일
```
tests/
└── Ypopup.Core.Tests/
    ├── Ypopup.Core.Tests.csproj           # xUnit + InternalsVisibleTo 대상
    ├── Protocol/
    │   └── PacketCodecTests.cs            # 패킷 직렬화·크기 제한·스트림 종료
    ├── Sharing/
    │   └── SharedFolderPathHelperTests.cs # 경로 정규화·트래버설 방지
    ├── IO/
    │   └── FileNameSanitizerTests.cs      # 파일명 sanitize·중복 이름 처리
    ├── Settings/
    │   └── SettingsMigrationTests.cs      # 레거시 경로 마이그레이션
    └── Transfers/
        ├── FileSendProgressTests.cs        # WriteFileAsync 진행률·취소 (MemoryStream)
        └── FileReceiveProgressTests.cs    # SaveFileAsync 진행률·취소 (MemoryStream)

src/Ypopup.Core/
├── IO/
│   └── FileNameSanitizer.cs               # Sanitize·GetUniquePath 추출 (public static)
├── Models/
│   └── TransferProgress.cs                # TransferProgress 레코드
└── Protocol/
    └── ProgressReporter.cs                # 임계값 기반 리포트 헬퍼 (1MB 단위 throttling)

src/Ypopup.Desktop/Views/Compose/
└── TransferProgressBar.axaml              # 재사용 가능한 진행률 바 UserControl
    (별도 파일: .axaml + .axaml.cs)

src/Ypopup.Desktop/Views/SharedFolder/
└── TransferProgressBar.axaml              # 같은 UserControl (공유 위치로 이동 검토)
```

> Compose·SharedFolder에서 같은 `TransferProgressBar`를 쓰므로 `src/Ypopup.Desktop/Controls/TransferProgressBar.axaml(.cs)`로 공통 배치.

변경 후 구조:
```
src/Ypopup.Desktop/Controls/
└── TransferProgressBar.axaml(.cs)         # 공용 진행률 바 + 취소 버튼
```

### 변경 파일 (요약)
| 파일 | 변경 요약 |
|------|-----------|
| `Ypopup.sln` | 테스트 프로젝트 + `tests` 솔루션 폴더 등록 |
| `src/Ypopup.Core/Ypopup.Core.csproj` | `InternalsVisibleTo("Ypopup.Core.Tests")` 추가 |
| `src/Ypopup.Core/Settings/SettingsService.cs` | `internal` 생성자 추가 (경로 주입) |
| `src/Ypopup.Core/Protocol/PacketCodec.cs` | `WriteFileAsync`/`SaveFileAsync`에 `IProgress<TransferProgress>?` 오버로드 추가 (기존 시그니처 유지) |
| `src/Ypopup.Network/Messaging/TcpHostService.cs` | `SanitizeFileName`/`GetUniquePath` → `FileNameSanitizer`로 교체, `SendMessageAsync`에 progress·token 오버로드 추가 |
| `src/Ypopup.Network/Sharing/SharedFolderClient.cs` | `DownloadBinaryAsync`에 `IProgress<TransferProgress>?`·Content-Length 파싱·`.partial` 패턴 |
| `src/Ypopup.Network/YpopupCoordinator.cs` | `SendMessageAsync`/`DownloadSharedFileAsync` 오버로드 추가 (기존 유지) |
| `src/Ypopup.Desktop/Views/Compose/ComposeWindow.axaml(.cs)` | `TransferProgressBar` 포함·`_cts`·UI 진행률 처리 |
| `src/Ypopup.Desktop/Views/SharedFolder/SharedFolderWindow.axaml(.cs)` | `TransferProgressBar` 포함·`_cts`·`.partial` cleanup |

---

## 4. 상세 구현 항목

### 단계 A1: 테스트 프로젝트骨架
- `tests/Ypopup.Core.Tests/Ypopup.Core.Tests.csproj`
  - `TargetFramework=net8.0`, `IsPackable=false`, `IsTestProject=true`
  - PackageReference: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`
  - ProjectReference: `src/Ypopup.Core/Ypopup.Core.csproj`
- `Ypopup.sln`: `tests` 솔루션 폴더(가상 폴더) 추가 + 프로젝트 등록 + Debug/Release 구성 매핑
- `src/Ypopup.Core/Ypopup.Core.csproj`에 `<InternalsVisibleTo Include="Ypopup.Core.Tests" />`

### 단계 A2: `FileNameSanitizer` 추출 (Core/IO)
- `public static class FileNameSanitizer`
  - `Sanitize(string fileName)` → `TcpHostService.SanitizeFileName` 내용 이동
  - `GetUniquePath(string path)` → `TcpHostService.GetUniquePath` 내용 이동
- `TcpHostService`에서 기존 private 메소드 제거, `FileNameSanitizer.*` 호출로 교체
- 기존 동작 100% 보존

### 단계 A3: `SettingsService` `internal` 생성자
- `internal SettingsService(string settingsDirectory)` 추가
- 기존 public 생성자는 `new SettingsService(AppDataDirectory)`로 위임
- 테스트에서 격리된 temp 디렉토리 사용

### 단계 A4: 단위 테스트 작성
- `PacketCodecTests`
  - Serialize/Deserialize 왕복
  - 잘못된 JSON → `InvalidDataException`
  - 크기 0·음수·16MB+1 → throw (`PacketCodec.cs:46`)
  - `ReadPacketAsync` 스트림 종료 → null 반환 또는 `EndOfStreamException`
  - `SaveFileAsync` 잘린 스트림 → `EndOfStreamException`
- `SharedFolderPathHelperTests`
  - 백슬래시/슬래시 정규화
  - `../` 트래버설 → throw
  - 빈 relativePath → root 반환
  - `ToRelativePath` 외부 경로 → throw
- `FileNameSanitizerTests`
  - invalid char → `_`
  - 빈/공백 → `received.bin`
  - 중복 없으면 원본 반환
  - 중복 시 `(1)`, `(2)` 패턴
- `SettingsMigrationTests`
  - legacy 수신 경로 → `exe\down`
  - legacy 공유 경로 (`Documents\Y-popup\공유폴더`, `publish\share`) → `exe\share`
  - 잘못된 공유 경로 → 기본값
- `FileSendProgressTests` / `FileReceiveProgressTests` (B단계 구현 후)
  - `MemoryStream` 기반, `Progress<TransferProgress>` 콜백 카운트·최종 100% 검증

### 단계 B1: `TransferProgress` 모델 + `ProgressReporter`
- `src/Ypopup.Core/Models/TransferProgress.cs`
  ```csharp
  public sealed record TransferProgress(long BytesTransferred, long TotalBytes, bool IsSending, string? FileName = null);
  ```
- `src/Ypopup.Core/Protocol/ProgressReporter.cs`
  - `sealed class ProgressThresholdReporter` — 1MB 단위로 `IProgress<TransferProgress>.Report` 호출 (너무 잦은 UI 갱신 방지)
  - `ReportIfThreshold(long totalBytes, long currentBytes, ...)` 인터페이스

### 단계 B2: `PacketCodec` 오버로드
- 기존 시그니처 유지
- 신규 오버로드:
  ```csharp
  WriteFileAsync(Stream, string filePath, CancellationToken, IProgress<TransferProgress>?)
  SaveFileAsync(Stream, string dest, long expectedSize, CancellationToken, IProgress<TransferProgress>?)
  ```
- 내부적으로 `ProgressThresholdReporter` 사용
- `CancellationToken` 이미 있음 (진행률만 추가)

### 단계 B3: `TcpHostService.SendMessageAsync` 오버로드
- 기존 시그니처 유지
- 신규:
  ```csharp
  SendMessageAsync(OutgoingMessage, AppSettings, CancellationToken, IProgress<TransferProgress>?)
  ```
- 다중 파일 `AttachmentPaths`를 순회하면서 파일별 progress 리셋/리포트
- `FileNameSanitizer`로 기존 private 교체

### 단계 B4: `SharedFolderClient.DownloadBinaryAsync` 확장
- `IProgress<TransferProgress>?` 매개변수 추가
- HTTP 응답에서 `Content-Length` 파싱 (`SharedFolderClient.cs:106-110` 주변)
- 임시 파일 패턴: destination + `.partial` → 완료 시 `File.Move(overwrite:false)`
- `OperationCanceledException` catch 시 `.partial` 삭제

### 단계 B5: `YpopupCoordinator` 오버로드
- `SendMessageAsync(OutgoingMessage, CancellationToken, IProgress<TransferProgress>?)`
- `DownloadSharedFileAsync(PeerInfo, string, string, CancellationToken, IProgress<TransferProgress>?)`
- 기존 1-arg/2-arg 시그니처는 신규 오버로드로 위임

### 단계 B6: `TransferProgressBar` UserControl (Desktop/Controls)
- `ProgressBar`, `TextBlock`(퍼센트/파일명), `Button`(취소)
- `DependencyProperty`:
  - `Progress` (double 0..1)
  - `FileName` (string)
  - `IsVisible`
  - `CancelCommand` (ICommand)
- single file에 몰지 않고 `.axaml`+`.axaml.cs` 분리

### 단계 B7: `ComposeWindow` 통합
- `TransferProgressBar` 컨트롤을 첨부 영역 아래 행에 배치
- `_CTS` 필드 → `SendButton_Click`에서 생성, `CancelButton` `Command` 바인딩
- `Progress<TransferProgress>` 인스턴스는 UI 스레드에서 생성 (`new Progress<...>` in event handler)
- 송신 완료/취소 시 ProgressBar 숨김·`_cts` dispose
- `IsAutoReply` 전송 (자동답장)에는 진행률 UI 비표시

### 단계 B8: `SharedFolderWindow` 통합
- `TransferProgressBar`를 하단 버튼 행 위에 배치
- `DownloadButton_Click` 비활성화, 진행률 표시
- `.partial` 패턴: `SharedFolderClient` 내부에서 처리하므로 UI는 취소 토큰만 전달
- 취소 시 안내 메시지

---

## 5. 실행 체크리스트

### Phase A (테스트 프로젝트)
- [x] A1: `tests/Ypopup.Core.Tests` 프로젝트·솔루션 등록
- [x] A2: `FileNameSanitizer` 추출·`TcpHostService` 교체
- [x] A3: `SettingsService` internal 생성자 + `InternalsVisibleTo`
- [x] A4: 단위 테스트 작성 (PacketCodec 8 / SharedFolderPathHelper 5 / FileNameSanitizer 6 / SettingsMigration 6)
- [x] A검증: `dotnet test` 25개 녹색

### Phase B (진행률·취소)
- [x] B1: `TransferProgress` + `ProgressReporter` 모델
- [x] B2: `PacketCodec` 오버로드
- [x] B3: `TcpHostService.SendMessageAsync` 오버로드
- [x] B4: `SharedFolderClient.DownloadBinaryAsync` 확장
- [x] B5: `YpopupCoordinator` 오버로드
- [x] B6: `TransferProgressBar` UserControl (Controls 폴더)
- [x] B7: `ComposeWindow` 통합
- [x] B8: `SharedFolderWindow` 통합
- [x] B9: `FileSendProgressTests`(3) + `FileReceiveProgressTests`(3) 작성
- [x] B검증: `dotnet test` 31개 녹성 + `dotnet build` 오류 없음

### 최종 검증
- [ ] `dotnet build` 경고 없음
- [ ] `dotnet test` 녹색
- [ ] UI 실동작 (2대 LAN 환경에서 송신/다운로드 진행률·취소 확인) — 별도 PC 필요

---

## 6. 위험·제약

| 위험 | 대응 |
|------|------|
| `Progress<T>.Report` 콜백이 UI 스레드에서 실행되지 않으면 크래시 | `Progress<T>` 인스턴스를 UI 스레드에서 `new` (Avalonia `Dispatcher.UIThread` 보장) |
| 1MB 진행률 임계값이 작은 파일에선 진행률이 안 보임 | 파일 < 1MB일 땐 시작·중간·완료 리포트 (전용 분기) |
| `CancellationToken` 전파 누락 시 취소해도 계속 전송 | 모든 `await` 지점에 token 전달 명시 |
| iOS/macOS 트레이 OS 분기 안 함 (기존 이슈) | 본 계획 범위 아님 |
| 송신 측 progress와 수신 측 실제 수신 바이트 다를 수 있음 (TCP 버퍼) | 송신 측에서 리포트하는 바이트가 "전송 요청 바이트" 임을 사용자 문구에 명시 |
| `.partial` 잔재 | catch 절에서 `File.Delete` + finally 보조 |

---

## 7. 진행 상태

| 단계 | 상태 | 비고 |
|------|------|------|
| 계획 수립 | 완료 | 2026-07-08 |
| 승인 | 대기 | 사용자 OK 후 진행 |
| Phase A | 완료 | 25개 단위 테스트 녹형 |
| Phase B | 완료 | 31개 단위 테스트 녹형 |
| 최종 검증 | 완료(코드) | `dotnet build` · `dotnet test` 녹색 |
| UI 실동작 검증 | 대기 | 2대 LAN 환경 필요 (별도 PC) |

---

## 8. 승인 후 진행 순서

1. Phase A1 → A2 → A3 → A4 → A검증 (`dotnet test` 녹색 확인)
2. Phase B1 → B2 → B3 → B4 → B5 → B9 (테스트) → B검증
3. Phase B6 → B7 → B8 → B검증
4. 전체 `dotnet build` + `dotnet test` 녹색
5. 사용자보고 + UI 동작 확인 가이드(2대 필요) 안내