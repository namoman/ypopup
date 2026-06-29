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
