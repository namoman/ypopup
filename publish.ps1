# Y-popup 배포: self-contained + framework-dependent 두 가지 exe
Get-Process -Name "Y-popup" -ErrorAction SilentlyContinue | Stop-Process -Force

powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\generate-app-icon.ps1"

Write-Host "=== 1/2 Self-contained (설치 불필요, ~70MB) ===" -ForegroundColor Cyan
dotnet publish src/Ypopup.App/Ypopup.App.csproj `
    -c Release `
    -r win-x64 `
    -o publish `
    /p:PublishSingleFile=true `
    /p:SelfContained=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== 2/2 Framework-dependent (.NET 8 Desktop Runtime 필요, ~5MB) ===" -ForegroundColor Cyan
dotnet publish src/Ypopup.App/Ypopup.App.csproj `
    -c Release `
    -r win-x64 `
    -o publish-framework `
    /p:PublishSingleFile=true `
    /p:SelfContained=false `
    /p:EnableCompressionInSingleFile=false `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== docs/ 복사 ===" -ForegroundColor Cyan
Copy-Item "publish\Y-popup.exe" "docs\Y-popup.exe" -Force
Copy-Item "publish-framework\Y-popup.exe" "docs\Y-popup-net8.exe" -Force

Get-Item "publish\Y-popup.exe", "publish-framework\Y-popup.exe", "docs\Y-popup.exe", "docs\Y-popup-net8.exe" |
    Format-Table @{N='Path';E={$_.FullName}}, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime
