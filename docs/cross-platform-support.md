# Y-popup 크로스플랫폼 지원 매트릭스

최종 검토일: 2026-07-08

Y-popup은 **Windows 10/11(64비트)** 와 **macOS(arm64/x64)** 만 공식 지원합니다.
Linux는 빌드/배포하지 않으므로 미지원입니다.

## 기능별 지원 현황

| 기능 | Windows | macOS | Linux | 비고 |
|------|:-------:|:-----:|:-----:|------|
| **트레이 아이콘** | ✅ | ⚠️ | ⚠️ | Avalonia `TrayIcon` 사용. macOS는 `.ico` 대신 PNG/템플릿 권장이라 시각적 문제 가능. |
| **시작프로그램 등록** | ✅ | ❌ | ❌ | Windows는 `HKCU\…\Run` 레지스트리. macOS/Linux는 `NullStartupService`(no-op). 설정 UI에서도 Windows만 표시. |
| **방화벽 규칙 자동 추가** | ✅ | ❌ | ❌ | Windows는 `netsh advfirewall`(UAC). macOS/Linux는 `StubFirewallService`(안내만). |
| **방화벽 설정 화면 열기** | ✅ | ❌ | ❌ | Windows는 `firewall.cpl`. macOS/Linux는 `PlatformNotSupportedException`. |
| **알림음(수신)** | ✅ | ⚠️ | ⚠️ | Windows는 `MessageBeep`. 비-Windows는 `Console.Beep` 폴백 — GUI 앱(`WinExe`)이라 미동작 가능성 있음. |
| **부재 자동 감지(유휴)** | ✅ | ❌ | ❌ | Windows는 `GetLastInputInfo`(P/Invoke). macOS/Linux는 `NullAwayIdleDetector`(`IsIdle` 항상 false). |
| **자동 답장** | ✅ | ✅ | ✅ | 부재 감지가 Windows 전용이므로 사실상 Windows에서만 작동. |
| **파일/폴더 다이얼로그** | ✅ | ✅ | ✅ | Avalonia `StorageProvider` 추상화. |
| **파일/링크 열기** | ✅ | ✅ | ✅ | `Process.Start(UseShellExecute=true)` — Windows는 ShellExecute, macOS는 `open`, Linux는 `xdg-open`. |
| **"탐색기/폴더 열기" 버튼** | ✅ | ✅ | ✅ | `UseShellExecute=true`로 통일됨 (이전에는 `explorer.exe` 직접 호출 — 비-Windows 실패). |
| **UDP 사용자 탐색** | ✅ | ✅ | ✅ | 플랫폼 무관. |
| **TCP 쪽지/파일 송수신** | ✅ | ✅ | ✅ | 플랫폼 무관. |
| **공유폴더(읽기 전용)** | ✅ | ✅ | ✅ | TCP HTTP 서버/클라이언트. 읽기 전용. |
| **진행률 표시·취소** | ✅ | ✅ | ✅ | `CancellationToken` + `TransferProgress`. |
| **TCP 동시 접속 제한** | ✅ | ✅ | ✅ | `ConnectionLimiter`(`SemaphoreSlim`). |
| **포트 변경 시 자동 재시작** | ✅ | ✅ | ✅ | Discovery/TcpHost/SharedFolder 일관적. |
| **백그라운드 실패 추적** | ✅ | ✅ | ✅ | `BackgroundTaskTracker`(`Debug.WriteLine` 로깅). |
| **설정 파일 경로** | `%AppData%\Y-popup\settings.json` | `~/Library/Application Support/Y-popup/settings.json` | `~/.config/Y-popup/settings.json` | `Environment.SpecialFolder.ApplicationData` 자동 매핑. |
| **패키징(`publish.ps1`)** | ✅ win-x64 | ✅ osx-arm64, osx-x64 | ❌ | Linux RID 빌드 없음. |
| **README 안내** | 상세 | 다운로드만 | ❌ | Linux는 언급 없음. |

## 범례

- ✅ 공식 지원 / 정상 동작
- ⚠️ 부분 동작 / 시각적 또는 환경 의존
- ❌ 미구현 / 미지원

## macOS/Linux 한계 참고사항

- **시작프로그램 등록**: macOS는 `osascript`/`LaunchAgent`(~/Library/LaunchAgents)로, Linux는 `~/.config/autostart/*.desktop`로 구현 가능하나 현재 `NullStartupService`로 no-op 처리.
- **알림음**: GUI 앱(`OutputType=WinExe`)이라 `Console.Beep` 폴백이 macOS에서 소리를 낼지 확신 불가. 필요 시 `NSBeep`/`AVAudioPlayer` 검토.
- **부재 자동 감지**: macOS는 `CGEventSourceSecondsSinceLastEventType` 등으로, Linux는 X11 idle 시간 등으로 구현 가능하나 현재 미구현.
- **방화벽**: macOS는 `pfctl` / 시스템 설정, Linux는 `ufw` / `firewalld`로 별도 구현 필요.
- **트레이 아이콘**: macOS는 `NSStatusItem` 템플릿 아이콘이 권장. 현재 `.ico`를 그대로 로드해 다크 모드/필터 적용 시 시각적 문제 가능.