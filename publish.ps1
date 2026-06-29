# Y-popup 완전 독립 단일 exe (.NET 런타임 별도 설치 불필요)
Get-Process -Name "Y-popup" -ErrorAction SilentlyContinue | Stop-Process -Force

powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\generate-app-icon.ps1"

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
