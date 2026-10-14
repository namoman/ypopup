# Y-popup 멀티챗 & 그룹화 마스터 로드맵 (TASK_ROADMAP.md)

| 태스크 ID | 구분 | 작업명 | 의존성 | 대상 파일 (최대 1~2개) | 상태 |
|---|---|---|---|---|:---:|
| **TASK-001** | Core | Core 도메인 기본 엔티티 정의 (`PeerUser`, `ChatMessage`, `MessageType`) | 없음 | `src/Ypopup.Core/Models/ChatEntities.cs` | [x] |
| **TASK-002** | Core | 멀티챗 및 그룹 엔티티 정의 (`ChatRoom`, `RoomType`, `UserGroup`) | TASK-001 | `src/Ypopup.Core/Models/RoomAndGroupEntities.cs` | [x] |
| **TASK-003** | Core | 확장 패킷 DTO 및 패킷 타입 정의 (`ExtendedLanPacket`, `ExtendedPacketType`) | TASK-001 | `src/Ypopup.Core/Models/ExtendedLanPacket.cs` | [x] |
| **TASK-004** | Core | 룸/그룹/스토리지 서비스 계약 인터페이스 정의 (`IChatRoomManager`, `IGroupManager`, `IChatStorageRepository`) | TASK-002 | `src/Ypopup.Core/Contracts/IChatContracts.cs` | [x] |
| **TASK-005** | Core | 확장 패킷 JSON 직렬화/역직렬화기 구현 (`ExtendedPacketCodec`) | TASK-003 | `src/Ypopup.Core/Protocol/ExtendedPacketCodec.cs` | [x] |
| **TASK-006** | Network | RoomId 태그 기반 패킷 라우터 구현 (`PacketRouter`, `IPacketRouter`) | TASK-003, 005 | `src/Ypopup.Network/Messaging/PacketRouter.cs` | [x] |
| **TASK-007** | Network | 멀티캐스트 및 풀메쉬 발송 디스패처 구현 (`MessageDispatcher`, `IMessageDispatcher`) | TASK-005, 006 | `src/Ypopup.Network/Messaging/MessageDispatcher.cs` | [x] |
| **TASK-008** | Storage | JSON 파일 기반 로컬 영속성 리포지토리 구현 (`JsonChatStorageRepository`) | TASK-002, 004 | `src/Ypopup.Core/Storage/JsonChatStorageRepository.cs` | [x] |
| **TASK-009** | Core/Net | 인메모리 및 영속화 연동 `ChatRoomManager` 구현 | TASK-004, 008 | `src/Ypopup.Network/Messaging/ChatRoomManager.cs` | [x] |
| **TASK-010** | Core/Net | 부서 자동 파싱 및 커스텀 그룹 관리 `GroupManager` 구현 | TASK-004, 008 | `src/Ypopup.Network/Messaging/GroupManager.cs` | [x] |
| **TASK-011** | UI | 조직도/부서 트리 아코디언 뷰모델 구현 (`UserGroupTreeViewModel`, `GroupNodeItem`) | TASK-010 | `src/Ypopup.Desktop/ViewModels/UserGroupTreeViewModel.cs` | [x] |
| **TASK-012** | UI | 활성 대화방 리스트 뷰모델 구현 (`RoomListViewModel`, `RoomItemViewModel`) | TASK-009 | `src/Ypopup.Desktop/ViewModels/RoomListViewModel.cs` | [x] |
| **TASK-013** | UI | 단일/멀티 세션 대화방 뷰모델 구현 (`ChatSessionViewModel`, `MessageBubbleItem`) | TASK-001, 009 | `src/Ypopup.Desktop/ViewModels/ChatSessionViewModel.cs` | [x] |
| **TASK-014** | UI | 대화창 세션 매니저 (멀티 윈도우 / 단일 창 탭 전환 제어) (`ChatWindowManager`) | TASK-013 | `src/Ypopup.Desktop/Windows/ChatWindowManager.cs` | [x] |
| **TASK-015** | UI | 메인 사용자 목록 창에 부서 아코디언 트리 UI 컨트롤 연동 (`UserGroupTreeView.axaml`) | TASK-011 | `src/Ypopup.Desktop/Views/UserList/UserGroupTreeControl.axaml(.cs)` | [x] |
| **TASK-016** | UI | 멀티챗 대화방 창 Avalonia UI 구현 (`ChatRoomWindow.axaml`) | TASK-013, 014 | `src/Ypopup.Desktop/Views/Chat/ChatRoomWindow.axaml(.cs)` | [x] |
| **TASK-017** | UI | 단체 쪽지 발송 및 다자간 대화방 생성 다이얼로그 (`CreateRoomDialog.axaml`) | TASK-007, 009 | `src/Ypopup.Desktop/Views/Chat/CreateRoomDialog.axaml(.cs)` | [x] |
| **TASK-018** | Tests | 풀메쉬 루프백 메시지 송수신 및 라우팅 통합 테스트 (`MultiChatIntegrationTests`) | TASK-001~010 | `tests/Ypopup.Network.Tests/MultiChatIntegrationTests.cs` | [x] |

---

## 작업 수행 규칙
1. 작업은 위 번호 순서(TASK-001부터 순차적)로 진행합니다.
2. 한 태스크가 완료되면 반드시 `dotnet build` 및 `dotnet test`로 무결성을 확인합니다.
3. 완료된 태스크는 `[ ]`를 `[x]`로 갱신하고 `git commit`을 생성합니다.
