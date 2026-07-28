# ypopup 컴파일, 푸시 및 다운로드 링크 수정 결과

## 작업 진행 내용
1. `push-github.ps1`을 통한 컴파일 및 깃 푸시 완료 (`v2.1.0`)
2. 깃페이지 다운로드 링크 원인 점검 및 `docs/index.html` 내 macOS 다운로드 확장자 수정 (`.dmg` -> `.zip`)
3. 수정한 `docs/index.html` 깃 푸시 완료

## 깃페이지 다운로드 불가 원인 및 해결 방법
- **원인**: 
  1. 다운로드 버튼 링크(`https://github.com/namoman/ypopup/releases/latest/download/...`)에 해당하는 **GitHub Release**가 아직 생성/업로드되지 않았기 때문입니다. (`404 Not Found`)
  2. macOS 다운로드 링크의 확장자가 실제 생성된 파일(`.zip`)과 다르게 `.dmg`로 연결되어 있었습니다. (수정 완료)
- **해결 방법**:
  - GitHub Web 저장소 페이지 (`https://github.com/namoman/ypopup/releases/new`) 접속
  - Tag: `v2.1.0` 생성
  - local의 `release/` 폴더에 생성된 7개 실행 파일들을 Release Asset으로 직접 드래그앤드롭하여 업로드 후 게시(Publish Release)하시면 다운로드 링크가 즉시 작동합니다.
