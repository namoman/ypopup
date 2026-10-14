# Y-popup 멀티챗 & 부서/조직도 그룹화 업데이트 구현 내역

## 1. 작업 배경 및 목적
- **배경**: 병원 등 중소 오피스 환경에서 기존 1:1 단순 팝업(빨간전화기 X-Popup 오마주) 구조를 넘어, 부서별 조직도 분류, 단체 공지 발송, 실시간 다자간 회의/대화 요구 증가.
- **목적**:
  1. 사용자 목록의 부서/그룹별 아코디언 트리(접기/펼치기) 뷰 지원.
  2. 다중 수신자를 대상으로 한 1회성 단체 쪽지 일괄 발송(Multi-cast).
  3. 실시간 다자간 그룹 대화방(단톡방) 지원 (P2P Full-Mesh 소켓 기반).
  4. 중앙 서버 없이 순수 LAN 환경에서 기존 v2.x 클라이언트와 충돌 없는 하위 호환성 유지.

---

## 2. 아키텍처 및 계층별 변경 내역

### 2.1 Core 계층 (`src/Ypopup.Core`)
- **[Models/ChatEntities.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Core/Models/ChatEntities.cs)**:
  - `PeerUser`: LAN 피어의 정보 및 부서명, 접속 상태 모델.
  - `ChatMessage`: `RoomId`, `SenderId`, `MessageType`, 첨부파일 목록을 포함하는 불변 메시지 레코드.
- **[Models/RoomAndGroupEntities.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Core/Models/RoomAndGroupEntities.cs)**:
  - `ChatRoom`: 1:1 및 다자간 대화방(`Direct1on1`, `GroupChat`) 메타데이터.
  - `UserGroup`: 부서/커스텀 사용자 그룹 및 정렬 순서 모델.
- **[Models/ExtendedLanPacket.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Core/Models/ExtendedLanPacket.cs)**:
  - `ExtendedPacketType`: `GroupChatMessage(10)`, `RoomInvite(11)`, `RoomLeave(12)`, `MultiCastDirectNotice(13)`.
  - `ExtendedLanPacket`: 기존 `LanPacket`과 필드를 공유하며 `RoomId`, `ParticipantIds`를 추가 확장.
- **[Contracts/IChatContracts.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Core/Contracts/IChatContracts.cs)**:
  - `IChatRoomManager`, `IGroupManager`, `IChatStorageRepository` 서비스 계약 정의.
- **[Protocol/ExtendedPacketCodec.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Core/Protocol/ExtendedPacketCodec.cs)**:
  - 4바이트 빅엔디안 길이 헤더 + UTF-8 JSON 직렬화/역직렬화 및 16MB 초과 패킷 방어.
- **[Storage/JsonChatStorageRepository.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Core/Storage/JsonChatStorageRepository.cs)**:
  - `%AppData%\Y-popup\storage\`에 `rooms.json`, `groups.json`, `messages/{roomId}.jsonl` 형태로 비동기 락(`SemaphoreSlim`) 기반 영속화.

### 2.2 Network 계층 (`src/Ypopup.Network`)
- **[Messaging/PacketRouter.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Network/Messaging/PacketRouter.cs)**:
  - `ConcurrentDictionary`를 사용하여 인입된 패킷의 `RoomId`를 기반으로 활성 대화창 또는 백그라운드 리스너로 분기 라우팅.
- **[Messaging/MessageDispatcher.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Network/Messaging/MessageDispatcher.cs)**:
  - 중앙 서버 없이 발신자가 방 참여자 전원에게 병렬 TCP 소켓 연결(`Task.WhenAll`)을 맺어 패킷을 분배.
  - 오프라인 참여자가 있어도 예외 격리 후 `SendResult(IsSuccess = false)` 기록.
- **[Messaging/ChatRoomManager.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Network/Messaging/ChatRoomManager.cs)**:
  - 1:1 대화방 중복 생성 방지, 다자간 대화방 생성 및 저장소 연동.
- **[Messaging/GroupManager.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Network/Messaging/GroupManager.cs)**:
  - 커스텀 조직도/부서 그룹 추가/삭제 및 사용자 매핑 영속화.

### 2.3 Desktop UI 계층 (`src/Ypopup.Desktop`)
- **[ViewModels/UserGroupTreeViewModel.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/ViewModels/UserGroupTreeViewModel.cs)** & **[Views/UserList/UserGroupTreeControl.axaml](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/Views/UserList/UserGroupTreeControl.axaml)**:
  - 사용자 목록을 부서(`Group`)별로 자동 그룹화하고 `Expander`를 통해 접었다 폈다 할 수 있는 트리 아코디언 UI.
- **[ViewModels/RoomListViewModel.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/ViewModels/RoomListViewModel.cs)** & **[ViewModels/ChatSessionViewModel.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/ViewModels/ChatSessionViewModel.cs)**:
  - 대화방 목록 관리 및 메시지 버블(내 메시지/상대방 메시지), 엔터키 전송, 자동 스크롤 로직.
- **[Views/Chat/ChatRoomWindow.axaml](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/Views/Chat/ChatRoomWindow.axaml)**:
  - 실시간 채팅 윈도우 UI.
- **[Views/Chat/CreateRoomDialog.axaml](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/Views/Chat/CreateRoomDialog.axaml)**:
  - 체크박스 기반 피어 다중 선택, 전체 선택/해제, 검색 필터링 모달 다이얼로그 (단체 쪽지 및 그룹방 생성 공용).
- **[Windows/ChatWindowManager.cs](file:///e:/dev/projects/ypopup/src/Ypopup.Desktop/Windows/ChatWindowManager.cs)**:
  - 이미 열려 있는 `RoomId` 창의 중복 생성을 막고 포커스 활성화 관리.

---

## 3. 검증 결과
- **통합 테스트 ([MultiChatIntegrationTests.cs](file:///e:/dev/projects/ypopup/tests/Ypopup.Network.Tests/MultiChatIntegrationTests.cs))**:
  - `PacketRouter` 정상 분기 검증 통과
  - `JsonChatStorageRepository` 대화방 및 메시지 추가/조회 검증 통과
  - `ExtendedPacketCodec` 직렬화 무결성 검증 통과 (12/12 테스트 통과)
- **전체 솔루션 빌드**:
  - `dotnet build Ypopup.sln` 컴파일 오류 0개 확인.
