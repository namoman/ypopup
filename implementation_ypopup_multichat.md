# Y-popup 멀티챗 및 조직도/그룹화 확장 개발 계획

## 1. 개요
병원 등 실무 환경의 요구사항(부서별 조직도 트리, 단체 쪽지 일괄 발송, 다자간 그룹 채팅방)을 Y-popup의 경량 P2P LAN 철학을 유지하면서 구현하기 위한 마스터 계획서입니다.
실제 구현은 경량 모델(Fast/Mini)이 대화 맥락 없이도 파일 단위로 독립 작업할 수 있도록 설계되었습니다.

---

## 2. 산출 문서 목록

1. **[docs/ARCHITECTURE.md](file:///e:/dev/projects/ypopup/docs/ARCHITECTURE.md)**
   - P2P Full-Mesh 세션 라이프사이클
   - 부서 자동 분류 및 커스텀 그룹 트리 아키텍처
   - 메시지 라우팅 및 예외/오프라인 처리 전략

2. **[docs/API_CONTRACTS.md](file:///e:/dev/projects/ypopup/docs/API_CONTRACTS.md)**
   - 도메인 모델 (`ChatRoom`, `UserGroup`, `PeerUser`, `ChatMessage`)
   - 네트워크 확장 패킷 규격 (`ExtendedLanPacket`)
   - 핵심 인터페이스 (`IChatRoomManager`, `IGroupManager`, `IPacketRouter`, `IMessageDispatcher`, `IChatStorageRepository`)

3. **[docs/TASK_ROADMAP.md](file:///e:/dev/projects/ypopup/docs/TASK_ROADMAP.md)**
   - TASK-001부터 TASK-018까지 총 18단계의 원자적 태스크 현황 체크리스트

4. **[docs/TASK_CARDS.md](file:///e:/dev/projects/ypopup/docs/TASK_CARDS.md)**
   - 경량 모델에 바로 복사하여 투입할 수 있는 18개의 독립 작업 카드 (프롬프트 모음)

---

## 3. 진행 방법 (Workflow)
1. `git checkout -b feature/multichat-grouping`
2. [docs/TASK_ROADMAP.md](file:///e:/dev/projects/ypopup/docs/TASK_ROADMAP.md)에서 현재 빈 체크박스 `[ ]` 태스크 확인
3. [docs/TASK_CARDS.md](file:///e:/dev/projects/ypopup/docs/TASK_CARDS.md)에서 해당 번호의 프롬프트를 경량 모델에 전달
4. 생성된 코드를 프로젝트에 반영 후 `dotnet build` 확인
5. 체크박스를 `[x]`로 갱신하고 커밋
