# Ypopup

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

- Windows 10 / 11
- .NET 8 SDK (빌드 시) / .NET 8 Desktop Runtime (실행 시)

## 빌드 및 실행

```powershell
cd D:\sw\dev\Ypopup
dotnet restore
dotnet build
dotnet run --project src\Ypopup\Ypopup.csproj
```

## 배포용 단일 exe 만들기

```powershell
dotnet publish src\Ypopup\Ypopup.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

출력: `publish\Ypopup.exe`

## 사용 방법

1. 사용할 PC마다 `Ypopup.exe` 실행
2. 트레이 아이콘 클릭 → 사용자 목록 확인
3. 사용자 선택 → 쪽지 작성 → 전송
4. 파일은 **파일 첨부** 버튼 또는 드래그 앤 드롭

## Windows 11 방화벽

첫 실행 시 Windows 방화벽 허용 창이 뜨면 **허용**을 선택하세요.

- UDP **50505** — 사용자 탐색
- TCP **50506** — 메시지/파일 수신

네트워크 프로필이 **공용**이면 차단될 수 있으므로 **개인 네트워크**로 설정하는 것을 권장합니다.

## 설정 파일

`%AppData%\Ypopup\settings.json`

## 수신 파일 저장 위치

기본: `문서\Ypopup\Received`
