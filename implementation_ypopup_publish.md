# ypopup 25MB 업로드 용량 제한 원인 분석 및 해결 방안

## 작업 개요
GitHub에 Release 파일 업로드 중 발생한 `File size too big: 25 MB are allowed, 40 MB were attempted to upload` 오류의 원인을 분석하고 해결 방안을 수립하였습니다.

## 원인 분석
1. **GitHub 웹 드래그앤드롭 영역 착오**:
   - Release 설명란(Markdown 텍스트 박스) 또는 일반 웹 파일 업로드(`upload/main`) 영역에 파일을 올리면 **25MB 제한**이 적용됩니다.
   - 올바른 위치인 Release 하단의 **Attach binaries** 박스에 올리면 최대 **2GB**까지 가능합니다.
2. **독립 실행형 파일 용량 (43.9MB)**:
   - .NET 런타임을 포함한 `Y-popup.exe`는 43.9MB입니다.
   - `.NET 8 Runtime` 필요 버전인 `Y-popup-net8.exe` 및 `.zip` 파일들은 10MB~12MB 수준입니다.

## 해결 방안
- 올바른 Attach binaries 박스 사용 안내
- 10MB 대 경량 패키지(`net8` 버전) 활용 안내
- `gh` CLI를 이용한 자동 업로드 명령 안내
