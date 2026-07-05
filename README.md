# Y-popup (파란전화기)

2000년대 **X-Popup(빨간전화기)** 오마주 — Windows 11 호환 LAN 메신저입니다.  
같은 네트워크(같은 IP 대역)에 있는 PC끼리 쪽지·파일·공유폴더를 주고받을 수 있습니다.

**다운로드:** [https://namoman.github.io/ypopup/](https://namoman.github.io/ypopup/)

## 기능

- UDP 브로드캐스트 기반 사용자 자동 탐색
- 1:1 인스턴트 메시지 (팝업 수신)
- 파일 첨부 및 드래그 앤 드롭 전송
- **공유폴더** — LAN 사용자가 내 `share` 폴더에서 파일 다운로드 (읽기 전용)
- 부재 표시 및 자동 답장
- 시스템 트레이 상주 (파란 전화기 아이콘)
- 수신 알림음
- 환경 설정 (프로필, 네트워크, 일반, 부재)

## 요구 사항

| 플랫폼 | 비고 |
|--------|------|
| **Windows 10 / 11 (64비트)** | 메인 배포 대상 |
| **macOS (arm64 / x64)** | zip 배포 (GitHub Pages) |
| **.NET 8 SDK** | 소스 빌드 시에만 필요 |

## 프로젝트 구조

```
Ypopup/
├── Ypopup.sln
├── publish.ps1          # Windows + macOS 배포 → docs/
├── ref/icon.png         # exe·트레이 아이콘 원본
└── src/
    ├── Ypopup.Core/        # 모델, 프로토콜, 설정
    ├── Ypopup.Network/     # LAN 탐색, 메시지/파일, 공유폴더
    ├── Ypopup.Desktop/     # Avalonia UI (메인 실행 진입점)
    └── Ypopup.App/         # WPF UI (레거시·참고용)
```

| 모듈 | 역할 |
|------|------|
| **Ypopup.Core** | `AppSettings`, 프로토콜, `SettingsService` |
| **Ypopup.Network** | UDP 탐색, TCP 메시지/파일, 공유폴더 HTTP |
| **Ypopup.Desktop** | Avalonia — 트레이, 사용자 목록, 설정, **배포 exe** |

## 빌드 및 실행

```powershell
cd D:\sw\dev\Ypopup
dotnet build
dotnet run --project src\Ypopup.Desktop\Ypopup.Desktop.csproj -c Release
```

## 배포

```powershell
.\publish.ps1
```

실행 순서: **Y-popup 종료 → `bin/obj` 삭제 → `publish*` 삭제 → `docs` 배포 exe/zip 삭제 → 아이콘 재생성 → publish → `docs/` 복사**

GitHub까지 한 번에:

```powershell
.\push-github.ps1 -Message "release: v2.0 update"
```

`push-github.ps1`은 `publish.ps1` 실행 → `docs/`·소스 커밋 → `git push`까지 수행합니다. 빌드만 갱신했을 때는 `-SkipPublish`로 push만 할 수 있습니다.

| `docs/` (GitHub Pages) | 설명 |
|------------------------|------|
| `Y-popup.exe` | Windows Self-contained (~44MB). **.NET 설치 불필요** |
| `Y-popup-net8.exe` | Windows Framework-dependent. [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) 필요 |
| `Y-popup-osx-*.zip` | macOS (arm64 / Intel) |

`publish*` 폴더는 로컬 중간 산출물(`.gitignore`)이며, 저장소·Pages에는 `docs/`만 포함됩니다.

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

## 제작자 · 문의

| | |
|---|---|
| **제작** | namoman |
| **웹사이트** | [namoman.com](https://namoman.com) |
| **문의·제안** | [namolove@gmail.com](mailto:namolove@gmail.com) |
