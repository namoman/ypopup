# UDP 브로드캐스트 + 멀티캐스트 탐색

- 작성일: 2026-10-23
- 상태: 구현됨
- 원칙: 지금 `Announce` 패킷과 대역 브로드캐스트는 유지한다. 같은 패킷을 그룹 주소로 한 번 더 보낸다.

## 목표

같은 L2 구간에서 대역 브로드캐스트(`192.168.0.255`)가 막혀도, 그룹에 가입한 Y-popup끼리는 서로 보이게 한다.

다른 게이트웨이, 사내 VPN, 새 패킷 종류, 탐색 서버, 설정 화면 항목은 이번 범위가 아니다. 멀티캐스트 TTL은 1이라 첫 라우터를 넘지 않는다.

## 동작

3초마다 보내는 JSON은 지금 `PacketType.Announce` 그대로다. 목적지만 둘이다.

| 순서 | 목적지 | 실패 시 |
|------|--------|---------|
| 1 | 기존 대역 브로드캐스트. 없으면 `255.255.255.255` | 지금과 같이 주소별 경고 로그 |
| 2 | `239.255.50.5`:`DiscoveryPort` (기본 50505) | 경고만 남기고 브로드캐스트 결과는 유지 |

그룹 주소 `239.255.50.5`는 IPv4 Local Scope(`239.255.0.0/16`)라 같은 링크 안에 둔다. 상수는 `AppConstants.DiscoveryMulticastAddress`. 설정으로 바꾸지 않는다.

받는 소켓은 지금처럼 `0.0.0.0:DiscoveryPort`에 묶는다. 가입은 선택된 IP(설정값, 없으면 기존 `ResolvePreferredIp`)의 인터페이스 한 곳에만 한다. VPN·가상 어댑터에는 가입하지 않는다. 선택된 IP가 `127.0.0.1`이면 가입을 건너뛴다.

시작 시 소켓 옵션:

- `MulticastTimeToLive = 1`
- 보낼 인터페이스를 선택된 IP로 고정 (`MulticastInterface`)
- `JoinMulticastGroup(그룹, 선택된 IP)`

가입이 실패해도 탐색 서비스는 뜨고 브로드캐스트만 계속한다. 종료 시 가입되어 있으면 `DropMulticastGroup` 후 소켓을 닫는다.

`PreferredLocalIp`나 `DiscoveryPort`를 바꾸면 이미 Discovery가 재시작된다. 재시작 때 새 인터페이스로 다시 가입하면 된다. 재시작 조건은 추가하지 않는다.

브로드캐스트와 멀티캐스트로 같은 알림이 두 번 들어와도 `HandleAnnounce`가 내용이 같을 때 목록 갱신을 생략한다. 자기 `MachineId`도 이미 버린다. 수신 경로는 수정하지 않는다.

옛 버전은 그룹에 가입하지 않으므로 멀티캐스트 알림은 못 받는다. 브로드캐스트는 그대로라 같은 대역의 옛 버전과는 계속 보인다.

방화벽 포트는 그대로다. UDP 50505 허용이면 그룹 수신도 그 포트로 들어온다.

## 바꿀 파일

| 파일 | 내용 |
|------|------|
| `src/Ypopup.Core/Models/AppSettings.cs` | `DiscoveryMulticastAddress = "239.255.50.5"` |
| `src/Ypopup.Core/Network/DiscoveryAnnounceTargets.cs` | 신규. 브로드캐스트 주소 목록과 그룹 주소로 `IPEndPoint` 목록을 만든다. 대역 주소가 없으면 `255.255.255.255`를 넣는다. |
| `src/Ypopup.Network/Discovery/DiscoveryService.cs` | 가입, TTL, 송신 인터페이스, 그룹으로 한 번 더 전송, 종료 시 탈퇴, 가입 상태 문자열 |
| `src/Ypopup.Network/YpopupCoordinator.cs` | 진단 창이 읽을 가입 상태만 노출 |
| `src/Ypopup.Desktop/Views/Diagnostics/LanDiagnosticWindow.axaml(.cs)` | «멀티캐스트» 한 줄. 예: `239.255.50.5 (가입됨)` |
| `src/Ypopup.Core/Diagnostics/DiagnosticExporter.cs` | 같은 주소와 가입 상태 한 줄. `Generate`에 상태 문자열 인자를 추가한다. |
| `tests/Ypopup.Core.Tests/Network/DiscoveryAnnounceTargetsTests.cs` | 아래 단위 테스트 |

`DiscoveryService`의 송신부는 이 목록을 순서대로 보내고, 주소마다 예외를 삼킨다. 그룹 전송을 브로드캐스트 루프 안에 넣지 않는다.

## 테스트

단위 테스트만 자동으로 돌린다. CI에서 실제 IGMP 가입은 하지 않는다.

- 그룹 주소가 `239.255.0.0/16` 안에 있다.
- 대역 주소가 있으면 그 주소들 뒤에 그룹 주소가 하나 붙는다. 포트는 `DiscoveryPort`.
- 대역 주소가 없으면 `255.255.255.255`와 그룹 주소 둘이다.
- 그룹 주소는 한 번만 들어간다.

수동 확인:

1. 같은 대역 PC 둘. 서로 목록에 보인다. LAN 진단에 `239.255.50.5 (가입됨)`.
2. 선택된 IP를 바꾼 뒤 다시 가입된다.
3. 같은 대역의 기존 버전과도 서로 보인다.
4. VPN으로 다른 대역에 있는 PC는 목록에 없다. 이게 정상이다.

## 하지 않는 것

- `Announce` 필드 추가, 새 `PacketType`
- 멀티캐스트 주소·켜기 설정, 설정 탭 UI
- TTL을 1보다 크게, 라우터 너머 전달
- VPN 주소 목록, 상주 PC 명부
- 방화벽 규칙 추가
