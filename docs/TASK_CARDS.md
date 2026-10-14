# Y-popup 원자적 작업 명세서 모음 (TASK CARDS)

> **경량 모델 사용 지침**:
> 아래 각 Task Card의 `[경량 모델용 프롬프트]` 블록만 복사하여 Fast/Mini 모델에 전달하면 됩니다. 각 프롬프트는 단독 실행 가능한 완전한 명세와 컨텍스트를 담고 있습니다.

---

### [TASK-001] Core 도메인 기본 엔티티 정의

* **목적**: 대화 참여자, 대화 메시지, 메시지 타입 등 멀티챗의 기초 도메인 불변 모델 정의
* **생성 파일**: `src/Ypopup.Core/Models/ChatEntities.cs`
* **참조 계약**: `docs/API_CONTRACTS.md`
* **DoD**: 외부 패키지 참조 없이 순수 C# record로 정의되며 빌드 통과

````markdown
```
[작업 지시: TASK-001]
y-popup 메신저의 'Ypopup.Core' 프로젝트에 멀티챗 도메인의 기본 모델을 정의하세요.

파일 경로: src/Ypopup.Core/Models/ChatEntities.cs
네임스페이스: Ypopup.Core.Models

요구사항:
1. 외부 서드파티 라이브러리 참조 없이 순수 C# 12 / .NET 8 문법(record, enum)으로 작성하세요.
2. 다음 타입을 정의하세요:
   - MessageType (enum): Text = 0, Notice = 1, FileAttachment = 2, SystemNotice = 3
   - PeerUser (record):
     * string UserId
     * string DisplayName
     * string IpAddress
     * int TcpPort
     * string Group
     * bool IsAway
     * DateTime LastSeenUtc
   - ChatMessage (record):
     * string MessageId
     * string RoomId
     * string SenderId
     * string SenderName
     * string Content
     * MessageType Type
     * DateTime TimestampUtc
     * IReadOnlyList<FileAttachmentInfo> Attachments (null 전달 시 빈 List로 초기화)
3. 기존 FileAttachmentInfo는 LanModels.cs에 이미 존재하므로 참조하여 호환되도록 구성하세요.

완전하고 에러 없는 파일 전체 코드를 출력하세요.
```
````

---

### [TASK-002] 멀티챗 및 그룹 엔티티 정의

* **목적**: 대화방(1:1, 그룹방) 및 부서/조직도 그룹 엔티티 정의
* **의존 작업**: TASK-001
* **생성 파일**: `src/Ypopup.Core/Models/RoomAndGroupEntities.cs`
* **DoD**: 불변 레코드 정의, 기본값 및 컬렉션 null-safe 처리

````markdown
```
[작업 지시: TASK-002]
y-popup의 'Ypopup.Core' 프로젝트에 대화방과 사용자 그룹 모델을 정의하세요.

파일 경로: src/Ypopup.Core/Models/RoomAndGroupEntities.cs
네임스페이스: Ypopup.Core.Models

요구사항:
1. 외부 의존성 없는 C# 순수 record로 작성하세요.
2. 다음 타입을 정의하세요:
   - RoomType (enum): Direct1on1 = 0, GroupChat = 1
   - UserGroup (record):
     * string GroupId
     * string GroupName
     * IReadOnlyList<string> MemberUserIds
     * int SortOrder = 0
     * bool IsExpanded = true
     * 생성자에서 MemberUserIds가 null이면 Array.Empty<string>()으로 할당
   - ChatRoom (record):
     * string RoomId
     * string Title
     * RoomType Type
     * IReadOnlyList<string> ParticipantIds
     * DateTime CreatedAtUtc
     * DateTime LastMessageAtUtc
     * 생성자에서 ParticipantIds가 null이면 Array.Empty<string>()으로 할당
3. 모든 속성은 get-only(init)로 설계하세요.

완전한 파일 전체 코드를 출력하세요.
```
````

---

### [TASK-003] 확장 패킷 DTO 및 패킷 타입 정의

* **목적**: 기존 `LanPacket`과 호환되면서 그룹방 RoomId와 참가자 목록을 전달할 수 있는 확장 DTO 정의
* **의존 작업**: TASK-001
* **생성 파일**: `src/Ypopup.Core/Models/ExtendedLanPacket.cs`
* **DoD**: 기존 패킷과의 필드명 호환성 유지

````markdown
```
[작업 지시: TASK-003]
y-popup의 네트워크 패킷 확장을 위한 DTO 클래스를 정의하세요.

파일 경로: src/Ypopup.Core/Models/ExtendedLanPacket.cs
네임스페이스: Ypopup.Core.Models

요구사항:
1. ExtendedPacketType (enum):
   - Announce = 0, TextMessage = 1, FileData = 2
   - GroupChatMessage = 10, RoomInvite = 11, RoomLeave = 12, MultiCastDirectNotice = 13
2. ExtendedLanPacket (class):
   - Type (ExtendedPacketType, 기본값 TextMessage)
   - MessageId (string, 기본값 Guid.NewGuid().ToString())
   - SenderId (string)
   - SenderName (string)
   - Body (string)
   - TcpPort (int)
   - Group (string)
   - Attachments (List<FileAttachmentInfo>, 기본값 빈 리스트)
   - RoomId (string?, nullable)
   - RoomTitle (string?, nullable)
   - ParticipantIds (List<string>, 기본값 빈 리스트)
   - SentAtUtc (DateTime, 기본값 DateTime.UtcNow)
3. 기존 LanModels.cs의 FileAttachmentInfo 클래스를 그대로 재사용하세요.

완전한 C# 코드를 출력하세요.
```
````

---

### [TASK-004] 서비스 계약 인터페이스 정의

* **목적**: 대화방 관리자, 그룹 관리자, 스토리지 리포지토리 인터페이스 정의
* **의존 작업**: TASK-002, TASK-003
* **생성 파일**: `src/Ypopup.Core/Contracts/IChatContracts.cs`
* **DoD**: docs/API_CONTRACTS.md의 메서드 시그니처와 100% 일치

````markdown
```
[작업 지시: TASK-004]
y-popup Core 계층에 멀티챗과 그룹 기능을 위한 인터페이스 명세서를 작성하세요.

파일 경로: src/Ypopup.Core/Contracts/IChatContracts.cs
네임스페이스: Ypopup.Core.Contracts

요구사항:
1. Ypopup.Core.Models의 모델들을 참조하세요.
2. 다음 인터페이스들을 단일 파일에 정의하세요:
   - IChatRoomManager:
     * Task<ChatRoom> GetOrCreateDirectRoomAsync(string targetUserId, CancellationToken ct = default);
     * Task<ChatRoom> CreateGroupRoomAsync(string title, IEnumerable<string> participantIds, CancellationToken ct = default);
     * Task<ChatRoom?> GetRoomAsync(string roomId, CancellationToken ct = default);
     * Task<IReadOnlyList<ChatRoom>> GetActiveRoomsAsync(CancellationToken ct = default);
     * Task LeaveRoomAsync(string roomId, CancellationToken ct = default);
     * event Action<ChatRoom>? OnRoomCreatedOrUpdated;
     * event Action<string>? OnRoomRemoved;
   - IGroupManager:
     * Task<IReadOnlyList<UserGroup>> GetAllGroupsAsync(CancellationToken ct = default);
     * Task<UserGroup> CreateGroupAsync(string groupName, CancellationToken ct = default);
     * Task DeleteGroupAsync(string groupId, CancellationToken ct = default);
     * Task AssignUserToGroupAsync(string groupId, string userId, CancellationToken ct = default);
     * Task RemoveUserFromGroupAsync(string groupId, string userId, CancellationToken ct = default);
     * Task ReorderGroupsAsync(IEnumerable<string> orderedGroupIds, CancellationToken ct = default);
     * Task SetGroupExpandedAsync(string groupId, bool isExpanded, CancellationToken ct = default);
     * event Action? OnGroupsChanged;
   - IChatStorageRepository:
     * Task SaveRoomAsync(ChatRoom room, CancellationToken ct = default);
     * Task<ChatRoom?> LoadRoomAsync(string roomId, CancellationToken ct = default);
     * Task<IReadOnlyList<ChatRoom>> LoadAllRoomsAsync(CancellationToken ct = default);
     * Task DeleteRoomAsync(string roomId, CancellationToken ct = default);
     * Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default);
     * Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(string roomId, int count = 50, CancellationToken ct = default);
     * Task SaveGroupsAsync(IEnumerable<UserGroup> groups, CancellationToken ct = default);
     * Task<IReadOnlyList<UserGroup>> LoadGroupsAsync(CancellationToken ct = default);

완전한 파일 코드를 출력하세요.
```
````

---

### [TASK-005] JSON 기반 ExtendedPacketCodec 구현

* **목적**: ExtendedLanPacket을 스트림에 쓰고 읽는 고성능/안전 직렬화기 구현
* **의존 작업**: TASK-003
* **생성 파일**: `src/Ypopup.Core/Protocol/ExtendedPacketCodec.cs`
* **DoD**: 4바이트 빅엔디안 길이 헤더 처리, 16MB 제한 검증, 단위 테스트 작성 가능

````markdown
```
[작업 지시: TASK-005]
y-popup의 ExtendedLanPacket을 직렬화/역직렬화하고 TCP Stream과 통신하는 코덱을 구현하세요.

파일 경로: src/Ypopup.Core/Protocol/ExtendedPacketCodec.cs
네임스페이스: Ypopup.Core.Protocol

요구사항:
1. 기존 PacketCodec.cs와 동일한 프로토콜 구조(4바이트 Big-Endian Length + UTF-8 JSON Payload)를 유지합니다.
2. System.Text.Json (JsonNamingPolicy.CamelCase)을 사용합니다.
3. 메서드:
   - byte[] Serialize(ExtendedLanPacket packet)
   - ExtendedLanPacket Deserialize(byte[] payload)
   - Task WritePacketAsync(Stream stream, ExtendedLanPacket packet, CancellationToken cancellationToken)
   - Task<ExtendedLanPacket?> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
4. 패킷 페이로드 크기 16MB 초과 시 InvalidDataException을 발생시키세요.
5. 스트림이 정상 종료되면 null을 반환하고 중간에 끊기면 EndOfStreamException을 던지도록 안전하게 구현하세요.

완전한 C# 코드를 출력하세요.
```
````

---

### [TASK-006] RoomId 기반 패킷 라우터 구현

* **목적**: 인입된 패킷을 분석하여 활성 룸 핸들러 또는 미등록 패킷 핸들러로 스레드 안전하게 라우팅
* **의존 작업**: TASK-003, TASK-005
* **생성 파일**: `src/Ypopup.Network/Messaging/PacketRouter.cs`
* **DoD**: ConcurrentDictionary 사용, 스레드 안전성 보장, 예외 격리

````markdown
```
[작업 지시: TASK-006]
네트워크 계층에서 수신된 패킷을 적절한 대화방으로 분기하는 PacketRouter를 구현하세요.

파일 경로: src/Ypopup.Network/Messaging/PacketRouter.cs
네임스페이스: Ypopup.Network.Messaging

인터페이스 및 클래스 요구사항:
public interface IPacketRouter
{
    void RegisterRoomHandler(string roomId, Action<ExtendedLanPacket> handler);
    void UnregisterRoomHandler(string roomId);
    void RouteIncomingPacket(ExtendedLanPacket packet);
    event Action<ExtendedLanPacket>? OnUnregisteredRoomPacketReceived;
    event Action<ExtendedLanPacket>? OnDirectMessageReceived;
}

public sealed class PacketRouter : IPacketRouter
{
    // 세부 구현
}

구현 지침:
1. 핸들러 저장은 ConcurrentDictionary<string, Action<ExtendedLanPacket>>을 사용하세요.
2. RouteIncomingPacket 처리 로직:
   - packet.RoomId가 없거나 비어있는 경우: OnDirectMessageReceived 이벤트 호출
   - packet.RoomId가 있고 등록된 핸들러가 있는 경우: 해당 Action<ExtendedLanPacket> 실행
   - packet.RoomId가 있지만 핸들러가 없는 경우: OnUnregisteredRoomPacketReceived 이벤트 호출
3. 핸들러 실행 시 try-catch로 감싸서 특정 방의 예외가 라우터 전체에 영향을 주지 않도록 하세요 (LogService.Error 호출).

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-007] 멀티챗 발송 디스패처 구현

* **목적**: 룸 참여자 전원(또는 다중 피어)에게 비동기 병렬/순차 TCP 소켓으로 패킷 발송
* **의존 작업**: TASK-005, TASK-006
* **생성 파일**: `src/Ypopup.Network/Messaging/MessageDispatcher.cs`
* **DoD**: 각 대상 피어별 성공/실패 여부를 담은 `SendResult` 반환

````markdown
```
[작업 지시: TASK-007]
여러 수신자에게 P2P TCP로 패킷을 발송하는 MessageDispatcher를 구현하세요.

파일 경로: src/Ypopup.Network/Messaging/MessageDispatcher.cs
네임스페이스: Ypopup.Network.Messaging

인터페이스 및 DTO 요구사항:
public sealed record SendResult(string PeerId, bool IsSuccess, string? ErrorMessage = null);

public interface IMessageDispatcher
{
    Task<IReadOnlyList<SendResult>> DispatchRoomMessageAsync(
        string roomId,
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default);

    Task<IReadOnlyList<SendResult>> MultiCastNoticeAsync(
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default);
}

구현 지침:
1. TcpClient를 사용해 각 targetPeer의 IpAddress와 TcpPort로 접속합니다 (연결 타임아웃 3초).
2. ExtendedPacketCodec.WritePacketAsync를 사용해 패킷을 전송합니다.
3. Task.WhenAll을 활용하여 대상 피어들에 동시 병렬 발송하되, 개별 피어의 소켓 에러(오프라인 등)는 해당 피어의 SendResult(IsSuccess = false)로 수집하고 전체 작업은 계속 진행되도록 작성하세요.
4. 모든 리소스(TcpClient, NetworkStream)는 using으로 안전하게 해제하세요.

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-008] 로컬 파일 기반 영속성 저장소 구현

* **목적**: 대화방 정보, 대화 로그, 사용자 그룹 설정을 로컬 JSON 파일로 영속화
* **의존 작업**: TASK-002, TASK-004
* **생성 파일**: `src/Ypopup.Core/Storage/JsonChatStorageRepository.cs`
* **DoD**: `%AppData%/Y-popup/` 디렉터리 기반 동작, SemaphoreSlim 비동기 파일 Lock 적용

````markdown
```
[작업 지시: TASK-008]
IChatStorageRepository를 구현하는 JsonChatStorageRepository를 작성하세요.

파일 경로: src/Ypopup.Core/Storage/JsonChatStorageRepository.cs
네임스페이스: Ypopup.Core.Storage

요구사항:
1. IChatStorageRepository 인터페이스를 구현합니다.
2. 기본 저장 경로는 Environment.GetFolderPath(SpecialFolder.ApplicationData) / "Y-popup" / "storage" 입니다.
3. 생성자에서 basePath를 주입받을 수 있도록 오버로드를 제공하세요 (테스트 격리 지원).
4. 파일 구조:
   - basePath/groups.json : UserGroup 목록
   - basePath/rooms.json : ChatRoom 메타데이터 목록
   - basePath/messages/{roomId}.jsonl : 메시지 단위로 한 줄씩 JSON 추가 (AppendAsync)
5. 비동기 파일 접근 충돌을 방지하기 위해 파일별 또는 전역 SemaphoreSlim을 활용하세요.
6. System.Text.Json을 사용하며 한글이 깨지지 않도록 JavaScriptEncoder.UnsafeRelaxedJsonEscaping 옵션을 적용하세요.

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-009] 인메모리 & 스토리지 연동 ChatRoomManager 구현

* **목적**: 1:1 대화방 조회/생성, 그룹 대화방 생성 및 활성 룸 캐시 관리
* **의존 작업**: TASK-004, TASK-008
* **생성 파일**: `src/Ypopup.Network/Messaging/ChatRoomManager.cs`
* **DoD**: 1:1 대화방 중복 생성 방지(참여자 쌍 기준 식별), 방 생성 이벤트 발행

````markdown
```
[작업 지시: TASK-009]
대화방 수명주기와 영속화를 관리하는 ChatRoomManager를 구현하세요.

파일 경로: src/Ypopup.Network/Messaging/ChatRoomManager.cs
네임스페이스: Ypopup.Network.Messaging

요구사항:
1. IChatRoomManager 인터페이스를 구현합니다.
2. 생성자에서 IChatStorageRepository와 내 사용자 ID(myUserId)를 주입받습니다.
3. 내부적으로 ConcurrentDictionary<string, ChatRoom> 캐시를 유지합니다.
4. GetOrCreateDirectRoomAsync:
   - 상대방 targetUserId와의 1:1 방이 캐시 또는 저장소에 이미 존재하면 해당 방 반환
   - 없으면 새 RoomId(Guid)를 발급하여 생성 후 저장소에 저장 및 OnRoomCreatedOrUpdated 호출
5. CreateGroupRoomAsync:
   - 참여자 목록에 myUserId가 누락되었으면 자동으로 포함
   - 새 그룹 ChatRoom 생성 후 저장소 저장 및 이벤트 발행
6. LeaveRoomAsync:
   - 참여자 목록에서 myUserId를 제거하거나 방을 닫고 OnRoomRemoved 이벤트 호출

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-010] 부서 자동 분류 및 커스텀 GroupManager 구현

* **목적**: 피어의 UDP `Group` 문자열 자동 동기화 및 사용자 커스텀 그룹(태그/즐겨찾기) CRUD 지원
* **의존 작업**: TASK-004, TASK-008
* **생성 파일**: `src/Ypopup.Network/Messaging/GroupManager.cs`
* **DoD**: 그룹 순서 변경, 사용자 배정, 변경 이벤트 발행

````markdown
```
[작업 지시: TASK-010]
사용자 그룹(부서 및 커스텀 조직도)을 관리하는 GroupManager를 구현하세요.

파일 경로: src/Ypopup.Network/Messaging/GroupManager.cs
네임스페이스: Ypopup.Network.Messaging

요구사항:
1. IGroupManager 인터페이스를 구현합니다.
2. 생성자에서 IChatStorageRepository를 주입받습니다.
3. 내부 캐시로 List<UserGroup>을 보관하며 스레드 락으로 보호합니다.
4. 주요 메서드:
   - CreateGroupAsync: 새 UserGroup 추가 및 저장
   - DeleteGroupAsync: 그룹 제거 및 저장
   - AssignUserToGroupAsync: 특정 그룹의 MemberUserIds에 userId 추가 (중복 방지)
   - RemoveUserFromGroupAsync: 그룹에서 userId 제외
   - ReorderGroupsAsync: 그룹 순서 갱신
   - SetGroupExpandedAsync: 접기/펼치기 상태 저장
5. 변경 발생 시 OnGroupsChanged 이벤트를 호출합니다.

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-011] 조직도/부서 트리 뷰모델 구현

* **목적**: Avalonia UI의 TreeView 또는 Expander ItemsControl에 바인딩할 그룹 노드 및 사용자 노드 뷰모델 정의
* **의존 작업**: TASK-010
* **생성 파일**: `src/Ypopup.Desktop/ViewModels/UserGroupTreeViewModel.cs`
* **DoD**: INotifyPropertyChanged 지원, 그룹별 온라인/총 인원 카운트 계산

````markdown
```
[작업 지시: TASK-011]
사용자 목록을 부서/그룹별로 접고 펼칠 수 있도록 지원하는 뷰모델을 구현하세요.

파일 경로: src/Ypopup.Desktop/ViewModels/UserGroupTreeViewModel.cs
네임스페이스: Ypopup.Desktop.ViewModels

요구사항:
1. INotifyPropertyChanged를 구현하거나 Avalonia의 ViewModelBase를 상속하세요.
2. 다음 모델을 포함하세요:
   - GroupNodeItem:
     * string GroupId
     * string GroupName
     * bool IsExpanded
     * ObservableCollection<PeerNodeItem> Members
     * string HeaderText => $"{GroupName} ({OnlineCount}/{Members.Count})"
   - PeerNodeItem:
     * PeerInfo Peer
     * string DisplayName
     * bool IsOnline
     * bool IsAway
3. UserGroupTreeViewModel:
   - ObservableCollection<GroupNodeItem> Groups { get; }
   - void UpdatePeers(IEnumerable<PeerInfo> peers) : 피어 목록이 갱신될 때 그룹별로 분류
   - void ToggleGroup(GroupNodeItem group) : 접기/펼치기 토글

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-012] 활성 대화방 리스트 뷰모델 구현

* **목적**: 참여 중인 대화방 목록과 마지막 메시지, 안 읽은 알림 상태를 표현하는 뷰모델
* **의존 작업**: TASK-009
* **생성 파일**: `src/Ypopup.Desktop/ViewModels/RoomListViewModel.cs`
* **DoD**: 실시간 룸 갱신 처리, 더블클릭 이벤트 커맨드 제공

````markdown
```
[작업 지시: TASK-012]
참여 중인 대화방 목록을 표시하는 RoomListViewModel을 구현하세요.

파일 경로: src/Ypopup.Desktop/ViewModels/RoomListViewModel.cs
네임스페이스: Ypopup.Desktop.ViewModels

요구사항:
1. RoomItemViewModel:
   * string RoomId
   * string Title
   * RoomType Type
   * string LastMessage
   * DateTime LastMessageTime
   * int UnreadCount
   * int ParticipantCount
2. RoomListViewModel:
   * ObservableCollection<RoomItemViewModel> Rooms { get; }
   * RoomItemViewModel? SelectedRoom { get; set; }
   * ICommand OpenRoomCommand { get; }
   * ICommand LeaveRoomCommand { get; }
   * IChatRoomManager와 연동하여 룸 목록을 로드하고 업데이트 이벤트 수신 시 자동 갱신
3. 스레드 안전하게 ObservableCollection을 조작하세요 (Dispatcher.UIThread).

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-013] 대화 세션 및 메시지 버블 뷰모델 구현

* **목적**: 대화방 내부에서 주고받은 메시지 목록, 입력창, 전송 상태를 관리하는 뷰모델
* **의존 작업**: TASK-001, TASK-009
* **생성 파일**: `src/Ypopup.Desktop/ViewModels/ChatSessionViewModel.cs`
* **DoD**: 송수신 메시지 버블 정렬, 입력 후 전송 커맨드, 파일 첨부 목록 바인딩

````markdown
```
[작업 지시: TASK-013]
단일 대화방의 메시지 뷰와 송수신을 담당하는 ChatSessionViewModel을 구현하세요.

파일 경로: src/Ypopup.Desktop/ViewModels/ChatSessionViewModel.cs
네임스페이스: Ypopup.Desktop.ViewModels

요구사항:
1. MessageBubbleItem:
   * string MessageId
   * string SenderId
   * string SenderName
   * string Content
   * DateTime Time
   * bool IsMine (내가 보낸 메시지 여부)
   * IReadOnlyList<FileAttachmentInfo> Attachments
2. ChatSessionViewModel:
   * ChatRoom Room { get; }
   * ObservableCollection<MessageBubbleItem> Messages { get; }
   * string InputText { get; set; }
   * bool CanSend => !string.IsNullOrWhiteSpace(InputText);
   * ICommand SendCommand { get; }
   * ICommand AttachFileCommand { get; }
   * MessageDispatcher 및 IChatStorageRepository와 연동하여 발송 시 메시지 저장 및 디스패치
   * 수신 메시지 인입 시 Messages 컬렉션에 추가

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-014] 대화창 세션 매니저 구현

* **목적**: 대화방을 독립 창(Multi-Window)으로 띄우거나 이미 열린 창을 포커스하는 윈도우 관리자
* **의존 작업**: TASK-013
* **생성 파일**: `src/Ypopup.Desktop/Windows/ChatWindowManager.cs`
* **DoD**: 동일한 RoomId의 창 중복 오픈 방지, 닫힘 이벤트 처리

````markdown
```
[작업 지시: TASK-014]
대화창의 중복 실행 방지 및 창 수명주기를 관리하는 ChatWindowManager를 작성하세요.

파일 경로: src/Ypopup.Desktop/Windows/ChatWindowManager.cs
네임스페이스: Ypopup.Desktop.Windows

요구사항:
1. IChatWindowManager 인터페이스 및 ChatWindowManager 클래스 구현
2. 메서드:
   - void OpenRoomWindow(string roomId, ChatRoom room)
   - void CloseRoomWindow(string roomId)
   - bool IsRoomOpen(string roomId)
3. Dictionary<string, Window> 형태로 열린 창 참조 보관.
4. 이미 열려 있는 roomId 요청 시 새로 창을 띄우지 않고 기존 Window.Activate() 호출.
5. Window.Closed 이벤트 발생 시 딕셔너리에서 자동 제거.

완전한 C# 코드를 작성하세요.
```
````

---

### [TASK-015] 사용자 목록 창 아코디언 트리 UI 연동

* **목적**: 기존 `UserListWindow`에 부서/그룹별 아코디언 접기/펼치기 UI 추가
* **의존 작업**: TASK-011
* **생성 파일**: `src/Ypopup.Desktop/Views/UserList/UserGroupTreeControl.axaml` 및 `.axaml.cs`
* **DoD**: Expander를 활용한 부서별 접기/펼치기, 더블클릭 시 대화 시작

````markdown
```
[작업 지시: TASK-015]
사용자를 그룹(부서)별로 묶어 접었다 펼쳤다 할 수 있는 Avalonia UserControl을 구현하세요.

파일 경로:
- src/Ypopup.Desktop/Views/UserList/UserGroupTreeControl.axaml
- src/Ypopup.Desktop/Views/UserList/UserGroupTreeControl.axaml.cs
네임스페이스: Ypopup.Desktop.Views.UserList

요구사항:
1. FluentTheme 스타일에 맞는 깔끔하고 모던한 UI를 구성하세요.
2. ItemsControl 내부에 Expander를 두고, 각 Expander의 Header는 부서명과 인원수를 표시하세요.
3. Expander의 Content로 ListBox를 배치하여 해당 부서 소속 피어 목록(프로필 아이콘, 닉네임, 상태)을 표시하세요.
4. 사용자 항목 더블클릭(DoubleTapped) 시 해당 사용자와의 대화 시작 이벤트(PeerSelected)를 발생시키세요.

axaml 파일과 axaml.cs 파일의 전체 코드를 각각 출력하세요.
```
````

---

### [TASK-016] 멀티챗 대화방 창 Avalonia UI 구현

* **목적**: 카카오톡/팀챗 스타일의 대화방 윈도우 UI (메시지 버블, 입력창, 전송 버튼)
* **의존 작업**: TASK-013, TASK-014
* **생성 파일**: `src/Ypopup.Desktop/Views/Chat/ChatRoomWindow.axaml` 및 `.axaml.cs`
* **DoD**: 내 메시지(우측 정렬/파란색 버블), 상대 메시지(좌측 정렬/회색 버블), 엔터키 전송

````markdown
```
[작업 지시: TASK-016]
실시간 대화방 윈도우(ChatRoomWindow)의 Avalonia UI를 작성하세요.

파일 경로:
- src/Ypopup.Desktop/Views/Chat/ChatRoomWindow.axaml
- src/Ypopup.Desktop/Views/Chat/ChatRoomWindow.axaml.cs
네임스페이스: Ypopup.Desktop.Views.Chat

요구사항:
1. 커스텀 타이틀바(방 이름, 참가자 수, 닫기 버튼)
2. 메시지 버블 리스트:
   - 내 메시지: 우측 정렬, 강조 색상 배경
   - 상대방 메시지: 좌측 정렬, 발신자 이름 표시, 연한 배경
   - 첨부 파일 표시 카드
3. 하단 입력 영역: TextBox(AcceptsReturn=False, Enter 키로 전송), 파일 첨부 버튼, 전송 버튼
4. 스크롤 뷰어가 새 메시지 인입 시 맨 아래로 자동 스크롤되도록 코드 비하인드에 보조 로직 작성.

axaml과 axaml.cs의 전체 코드를 출력하세요.
```
````

---

### [TASK-017] 단체 쪽지 및 그룹 대화방 생성 다이얼로그

* **목적**: 여러 피어를 체크박스로 다중 선택하여 단체 쪽지를 보내거나 새 그룹방을 만드는 팝업
* **의존 작업**: TASK-007, TASK-009
* **생성 파일**: `src/Ypopup.Desktop/Views/Chat/CreateRoomDialog.axaml` 및 `.axaml.cs`
* **DoD**: 전체 선택/해제, 검색 필터링, 선택된 피어 목록 전달

````markdown
```
[작업 지시: TASK-017]
다수의 참여자를 선택하여 단체 쪽지를 발송하거나 그룹 대화방을 개설하는 모달 다이얼로그를 작성하세요.

파일 경로:
- src/Ypopup.Desktop/Views/Chat/CreateRoomDialog.axaml
- src/Ypopup.Desktop/Views/Chat/CreateRoomDialog.axaml.cs
네임스페이스: Ypopup.Desktop.Views.Chat

요구사항:
1. 상단: 방 제목 입력창 (그룹방인 경우) 및 검색창
2. 중단: 체크박스가 포함된 피어 목록 (부서명, 이름 표기)
3. "전체 선택" / "선택 해제" 편의 버튼 제공
4. 하단: "취소", "확인(초대/발송)" 버튼
5. ShowDialogAsync로 호출 시 선택된 피어 목록과 방 제목을 결과 객체(CreateRoomResult)로 반환.

axaml과 axaml.cs의 전체 코드를 출력하세요.
```
````

---

### [TASK-018] 종합 통합 루프백 테스트 구현

* **목적**: 가상 피어 노드 간 패킷 라우팅 및 멀티캐스트 디스패치 루프백 검증
* **의존 작업**: TASK-001 ~ TASK-010
* **생성 파일**: `tests/Ypopup.Network.Tests/MultiChatIntegrationTests.cs`
* **DoD**: xUnit 테스트 3개 이상 작성, 패킷 직렬화 및 라우팅 성공 검증

````markdown
```
[작업 지시: TASK-018]
멀티챗 패킷 라우팅 및 메시지 분배를 검증하는 xUnit 단위/통합 테스트를 작성하세요.

파일 경로: tests/Ypopup.Network.Tests/MultiChatIntegrationTests.cs
네임스페이스: Ypopup.Network.Tests

요구사항:
1. PacketRouterTests:
   - 특정 RoomId로 등록된 핸들러에 패킷이 정상 도달하는지 검증
   - 알 수 없는 RoomId 수신 시 OnUnregisteredRoomPacketReceived 이벤트가 발생하는지 검증
2. JsonChatStorageRepositoryTests:
   - 임시 디렉터리를 생성하여 방 생성, 메시지 Append, 조회 후 일치 여부 검증
3. ExtendedPacketCodecTests:
   - ExtendedLanPacket의 다자간 필드(RoomId, ParticipantIds) 직렬화 및 역직렬화 동등성 검증

xUnit과 FluentAssertions를 사용하여 완전한 테스트 코드를 작성하세요.
```
````
