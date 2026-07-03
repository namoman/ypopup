# Y-popup

2000년대 **X-Popup(빨간전화기)** 오마주 — Windows 11 호환 LAN 메신저입니다.  
같은 네트워크(같은 IP 대역)에 있는 PC끼리 쪽지와 파일을 주고받을 수 있습니다.

## 기능

- UDP 브로드캐스트 기반 사용자 자동 탐색
- 1:1 인스턴트 메시지 (팝업 수신)
- 파일 첨부 및 드래그 앤 드롭 전송
- 시스템 트레이 상주 (빨간 전화기 스타일 아이콘)
- 수신 알림음 및 풍선 알림
- 환경 설정 (표시 이름, 수신 폴더, 포트)

## 요구 사항

- Windows 10 / 11 (64비트)
- .NET 8 SDK (소스 빌드 시에만 필요)

## 프로젝트 구조 (기능별 모듈)

```
Ypopup/
├── Ypopup.sln
└── src/
    ├── Ypopup.Core/        # 모델, 프로토콜, 설정
    ├── Ypopup.Network/    # LAN 탐색, 메시지/파일 전송
    └── Ypopup.App/        # WPF UI (실행 진입점)
```

| 모듈 | 역할 |
|------|------|
| **Ypopup.Core** | `Models`, `Protocol`, `Settings` — UI/네트워크 공통 기반 |
| **Ypopup.Network** | `Discovery`, `Messaging`, `Coordinator` — LAN 통신 |
| **Ypopup.App** | 트레이, 창 UI — **단일 exe**로 배포 |

소스는 모듈별로 분리되어 있지만, 배포 시 **Y-popup.exe 하나**로 묶입니다.

## 빌드 및 실행

```powershell
cd D:\sw\dev\Ypopup
dotnet build
dotnet run --project src\Ypopup.App\Ypopup.App.csproj
```

## 배포 (두 가지 exe)

```powershell
.\publish.ps1
```

| 출력 (로컬, `.gitignore`) | 설명 |
|------|------|
| `publish\Y-popup.exe` | Self-contained (~44MB). **.NET 설치 불필요** |
| `publish-framework\Y-popup.exe` | Framework-dependent (~10MB). [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) 필요 |

`publish.ps1` 실행 후 **GitHub Pages·배포용**으로 `docs\`에만 복사됩니다 (`Y-popup.exe`, `Y-popup-net8.exe`, macOS zip 등). `publish*` 폴더는 빌드 중간 산출물이라 저장소에 포함하지 않습니다.

수동 빌드 (self-contained만):

```powershell
dotnet publish src\Ypopup.App\Ypopup.App.csproj -c Release -r win-x64 -o publish `
  /p:PublishSingleFile=true /p:SelfContained=true `
  /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
```

## 사용 방법

1. 사용할 PC마다 `Y-popup.exe` 실행
2. 트레이 아이콘 클릭 → 사용자 목록 확인
3. 사용자 선택 → 쪽지 작성 → 전송
4. 파일은 **파일 첨부** 버튼 또는 드래그 앤 드롭

## Windows 11 방화벽

첫 실행 시 Windows 방화벽 허용 창이 뜨면 **허용**을 선택하세요.

- UDP **50505** — 사용자 탐색
- TCP **50506** — 메시지/파일 수신

## 설정 파일

`%AppData%\Y-popup\settings.json`

## 수신 파일 저장 위치

기본: `문서\Y-popup\Received`

## 제작자 · 문의

| | |
|---|---|
| **제작** | namoman |
| **웹사이트** | [namoman.com](https://namoman.com) |
| **문의·제안** | [namolove@gmail.com](mailto:namolove@gmail.com) |
