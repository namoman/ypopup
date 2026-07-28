# ypopup 컴파일 및 GitHub 푸시 구현 계획

## 작업 개요
ypopup 프로젝트의 변경 사항(버전 2.1.0 업데이트 등)을 컴파일(퍼블리시)하고 git commit & push를 수행합니다.

## 실행 절차
1. `push-github.ps1` 스크립트 실행
   - `publish.ps1`을 통한 컴파일 및 게시 파일 생성
   - 변경된 파일 git add
   - git commit -m "Update version to 2.1.0"
   - git push origin main
2. 실행 결과 검증 및 상태 확인
