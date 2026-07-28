# ypopup Release 25MB 업로드 제한 해결 가이드

## 문제 상황
GitHub 웹페이지에서 파일 업로드 시 `File size too big: 25 MB are allowed, 40 MB were attempted to upload.` 에러가 발생함.

## 원인
1. GitHub 웹페이지 내 **텍스트 작성 상자(Markdown 드롭존)**나 일반 저장소 파일 추가 페이지에 파일을 떨어뜨렸을 경우 25MB 제한이 적용됩니다.
2. 독립 실행 파일(`Y-popup.exe`, 43.9MB)이 25MB를 초과함.

## 해결 방법

### 방법 A: 올바른 Attach Binaries 영역 이용 (추천)
- [GitHub New Release 페이지](https://github.com/namoman/ypopup/releases/new) 접속
- 페이지 맨 하단의 **"Attach binaries by dropping them here or selecting them"** 라고 적힌 회색 테두리 박스 영역에 40MB 실행 파일을 드래그앤드롭 (해당 영역은 최대 2GB까지 업로드 허용).

### 방법 B: 10MB 경량 패키지 업로드
- `.NET 8 Runtime` 용 패키지(`Y-popup-net8.exe`, `Y-popup-win-x64-net8.zip` 등)는 **10MB ~ 12MB**이므로 제한 없이 빠르게 업로드할 수 있습니다.

### 방법 C: GitHub CLI (`gh`) 사용
Terminal에서 `gh` 설치 후 자동 업로드:
```powershell
winget install GitHub.cli
gh auth login
gh release create v2.1.0 release/* --title "v2.1.0"
```
