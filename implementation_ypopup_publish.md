# ypopup 깃페이지 다운로드 링크 수정 및 원인 분석

## 작업 개요
깃페이지 다운로드 링크 미작동 원인을 분석하고, `docs/index.html` 내 링크 불일치를 수정하였습니다.

## 원인 분석
1. **GitHub Release 태그/Asset 미업로드 (404 Not Found)**
   - `docs/index.html`의 다운로드 버튼이 `https://github.com/namoman/ypopup/releases/latest/download/...` URL을 참조하고 있으나, GitHub 저장소에 `v2.1.0` Release 등록 및 실행 파일(Asset) 업로드가 진행되지 않아 404 에러가 발생합니다.
2. **macOS 링크 확장자 불일치 (.dmg vs .zip)**
   - `docs/index.html` 내 macOS 다운로드 링크가 `.dmg`로 작성되어 있었으나, 실제 컴파일 산출물(`release/`)은 `.zip` 형태로 생성됩니다.

## 수정 사항
- `docs/index.html`의 macOS 다운로드 링크 확장자를 `.dmg`에서 `.zip`으로 수정 및 git push 완료.
