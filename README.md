# Y-popup (파란전화기)

2000년대 **X-Popup(빨간전화기)** 오마주 — Windows 11 호환 LAN 메신저입니다.  
같은 네트워크(같은 IP 대역)에 있는 PC끼리 쪽지·파일·공유폴더를 주고받을 수 있습니다.

**다운로드:** [https://namoman.github.io/ypopup/](https://namoman.github.io/ypopup/)

## 기능

- UDP 브로드캐스트 기반 사용자 자동 탐색
- 1:1 인스턴트 메시지 (팝업 수신)
- 파일 첨부 및 드래그 앤 드롭 전송 — **진행률 표시 + 취소 가능**
- **공유폴더** — LAN 사용자가 내 `share` 폴더에서 파일 다운로드 (읽기 전용, 진행률 표시)
- 부재 표시 및 자동 답장
- 시스템 트레이 상주 (파란 전화기 아이콘)
- 수신 알림음
- **LAN 진단 화면** — 선택된 IP, 브로드캐스트 대상, 패킷 시간, 피어 목록
- **롤링 로그** — `%AppData%\Y-popup\logs` 일별 로그 파일
- 환경 설정 (프로필, 네트워크, 일반, 부재)

## 요구 사항

| 플랫폼 | 비고 |
|--------|------|
| **Windows 10 / 11 (64비트)** | 메인 배포 대상 |
| **macOS (arm64 / x64)** | zip 배포 (GitHub Pages). 일부 기능(시작프로그램, 방화벽, 부재 자동 감지, 알림음)은 Windows에서만 동작합니다. |
| **.NET 8 SDK** | 소스 빌드 시에만 필요 |

> 플랫폼별 지원 현황은 [docs/cross-platform-support.md](docs/cross-platform-support.md) 참조. Linux는 빌드/배포하지 않습니다.

## 프로젝트 구조

```
Ypopup/
├── Ypopup.sln
├── publish.ps1              # Windows + macOS 배포 → release/
├── push-github.ps1          # publish → git add → commit → push
├── .gitignore               # bin/obj/publish*/release/ 제외
├── ref/icon.png             # exe·트레이 아이콘 원본
├── docs/                    # GitHub Pages 웹페이지
│   ├── index.html
│   ├── screenshot.png
│   └── cross-platform-support.md
├── src/
│   ├── Ypopup.Core/         # 모델, 프로토콜, 설정, 로깅, 검증
│   ├── Ypopup.Network/      # LAN 탐색, 메시지/파일, 공유폴더
│   └── Ypopup.Desktop/      # Avalonia UI (메인 실행 진입점)
├── tests/
│   ├── Ypopup.Core.Tests/   # 단위 테스트 (xUnit, 67개)
│   └── Ypopup.Network.Tests/ # 통합 테스트 (xUnit, 8개)
└── tools/
    ├── generate-app-icon.ps1
    └── create-release.ps1   # gh release create 자동화
```

| 모듈 | 역할 |
|------|------|
| **Ypopup.Core** | `AppSettings`, 프로토콜, `SettingsService`, `LogService`, `SettingsValidator` |
| **Ypopup.Network** | UDP 탐색, TCP 메시지/파일, 공유폴더 HTTP, `ConnectionLimiter`, `BackgroundTaskTracker` |
| **Ypopup.Desktop** | Avalonia — 트레이, 사용자 목록, 설정, 공유폴더, LAN 진단, 진행률 UI |

## 빌드 및 실행

```powershell
cd D:\sw\dev\Ypopup
dotnet build
dotnet run --project src\Ypopup.Desktop\Ypopup.Desktop.csproj -c Release
```

## 배포

바이너리는 저장소에 포함하지 않고 **GitHub Releases**로 배포합니다.

```powershell
# 1. 바이너리 빌드
.\publish.ps1
```

`publish.ps1` 실행 순서: **Y-popup 종료 → `bin/obj` 삭제 → `publish*` 삭제 → 아이콘 재생성 → publish (win-x64 / osx-arm64 / osx-x64) → `release/`에 복사**

```powershell
# 2. GitHub Release 생성
.\tools\create-release.ps1 -Version "2.0.0"
```

`create-release.ps1`은 `release/` 폴더의 파일을 `gh release create`로 업로드합니다.

```powershell
# 3. 소스 코드만 git push
.\push-github.ps1 -Message "release: v2.0 update"
```

`push-github.ps1`은 `publish.ps1` 실행 → 소스·docs 웹페이지만 커밋 → `git push`까지 수행합니다. 빌드만 갱신했을 때는 `-SkipPublish`로 push만 할 수 있습니다.

| GitHub Releases | 설명 |
|-----------------|------|
| `Y-popup.exe` | Windows Self-contained (~44MB). **.NET 설치 불필요** |
| `Y-popup-net8.exe` | Windows Framework-dependent. [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) 필요 |
| `Y-popup-osx-*.zip` | macOS (arm64 / Intel) |

다운로드는 [GitHub Releases](https://github.com/namoman/ypopup/releases) 또는 [GitHub Pages](https://namoman.github.io/ypopup/)에서 가능합니다.

## 사용 방법

1. 사용할 PC마다 `Y-popup.exe` 실행
2. 트레이 아이콘 클릭 → **사용자 목록** 확인 (헤더에 본인 **표시 이름** 표시)
3. 사용자 선택 → **쪽지 보내기** 또는 더블클릭
4. 파일은 **파일 첨부** 또는 드래그 앤 드롭
5. 상대 목록의 **📁** 아이콘 → 공유폴더 탐색·다운로드

## 공유폴더

- **설정 > 일반**에서 «공유폴더 사용» 켜기
- 공유할 파일은 **실행 exe 옆 `share` 폴더**에 넣기 (기본 경로)
- 상대 PC에서 사용자 목록의 📁 버튼으로 접근
- 공유폴더가 비어 있으면 상대는 빈 목록만 보입니다 — `share` 경로를 설정에서 확인하세요

## Windows 방화벽

첫 실행 시 허용 창이 뜨면 **허용**을 선택하세요.  
차단했다면 **설정 > 네트워크 > 방화벽 허용 추가**를 사용하세요.

| 포트 | 용도 |
|------|------|
| UDP **50505** | 사용자 탐색 |
| TCP **50506** | 쪽지·파일 수신 |
| TCP **50507** | 공유폴더 |

## 설정 파일

`%AppData%\Y-popup\settings.json`

| 항목 | 기본값 |
|------|--------|
| 수신 파일 | `{exe 위치}\down` |
| 공유 폴더 | `{exe 위치}\share` |
| 표시 이름 | `홍길동` |

레거시 경로(`Documents\Y-popup\Received` 등)는 앱 시작 시 자동으로 위 기본값으로 마이그레이션됩니다.

## 아이콘 변경

`ref/icon.png`를 수정한 뒤:

```powershell
.\tools\generate-app-icon.ps1
.\publish.ps1
```

## 테스트

```powershell
dotnet test Ypopup.sln
```

| 프로젝트 | 개수 | 유형 |
|----------|------|------|
| `Ypopup.Core.Tests` | **67** | 단위 테스트 (PacketCodec, Settings, Progress, ConnectionLimiter, BackgroundTaskTracker, SettingsValidator, FileNameSanitizer, SharedFolderPathHelper) |
| `Ypopup.Network.Tests` | **8** | 통합 테스트 (SharedFolderHostService + SharedFolderClient 실제 TCP 통신) |

## 변경 로그

| 일자 | 변경 |
|------|------|
| 2026-07-09 | P2 LAN 진단 화면, P2 롤링 로그, P2 배포 산출물 정리, P3 설정 검증 중복 제거 |
| 2026-07-08 | P0 자동 테스트 (39개), P2-X 파일 전송 진행률+취소, P1 TCP 동시접속 제한·포트 일관성·백그라운드 실패 추적, P2-C 크로스플랫폼 문서, P3-X XAML 경고 확인 |
| 2026-07-05 | push-github.ps1, Windows 시작 시 트레이 전용 실행 |
| 2026-07-03 | 버전 2.0 릴리스, WPF→Avalonia 설정 패리티, 공유폴더 HTTP 서버, 사용자 목록 UI |
| 2026-06-29 | X-Popup 클론 초기 구현, 프로젝트 경로 이전, 모듈 분리, SelfContained 배포, 트레이 아이콘 |

## 제작자 · 문의

| | |
|---|---|
| **제작** | namoman |
| **웹사이트** | [namoman.com](https://namoman.com) |
| **문의·제안** | [namolove@gmail.com](mailto:namolove@gmail.com) |
