# Y-popup 배포: WPF(Windows) + Avalonia(크로스플랫폼) 각각 self-contained / framework-dependent
Get-Process -Name "Y-popup" -ErrorAction SilentlyContinue | Stop-Process -Force

powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\generate-app-icon.ps1"

$publishArgs = @(
    '-c', 'Release',
    '-r', 'win-x64',
    '/p:PublishSingleFile=true',
    '/p:DebugType=None',
    '/p:DebugSymbols=false'
)

function Publish-Project {
    param(
        [string]$Project,
        [string]$Output,
        [bool]$SelfContained,
        [bool]$Compress
    )
    $args = @('publish', $Project) + $publishArgs + @('-o', $Output)
    $args += "/p:SelfContained=$SelfContained"
    $args += "/p:IncludeNativeLibrariesForSelfExtract=$SelfContained"
    $args += "/p:EnableCompressionInSingleFile=$Compress"
    dotnet @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "=== 1/4 WPF Self-contained (Windows, ~70MB) ===" -ForegroundColor Cyan
Publish-Project -Project 'src/Ypopup.App/Ypopup.App.csproj' -Output 'publish-wpf' -SelfContained $true -Compress $true

Write-Host "=== 2/4 WPF Framework-dependent (Windows + .NET 8 Desktop Runtime, ~5MB) ===" -ForegroundColor Cyan
Publish-Project -Project 'src/Ypopup.App/Ypopup.App.csproj' -Output 'publish-wpf-framework' -SelfContained $false -Compress $false

Write-Host "=== 3/4 Avalonia Self-contained (Win x64, 설치 불필요, ~70MB) ===" -ForegroundColor Cyan
Publish-Project -Project 'src/Ypopup.Desktop/Ypopup.Desktop.csproj' -Output 'publish' -SelfContained $true -Compress $true

Write-Host "=== 4/4 Avalonia Framework-dependent (Win x64 + .NET 8 Runtime, ~15MB) ===" -ForegroundColor Cyan
Publish-Project -Project 'src/Ypopup.Desktop/Ypopup.Desktop.csproj' -Output 'publish-framework' -SelfContained $false -Compress $false

Write-Host "=== docs/ 복사 ===" -ForegroundColor Cyan
# 메인 다운로드: Avalonia (크로스플랫폼 UI)
Copy-Item "publish\Y-popup.exe" "docs\Y-popup.exe" -Force
Copy-Item "publish-framework\Y-popup.exe" "docs\Y-popup-net8.exe" -Force
# Windows WPF (레거시)
Copy-Item "publish-wpf\Y-popup.exe" "docs\Y-popup-wpf.exe" -Force
Copy-Item "publish-wpf-framework\Y-popup.exe" "docs\Y-popup-wpf-net8.exe" -Force

Get-Item `
    "publish\Y-popup.exe", `
    "publish-framework\Y-popup.exe", `
    "publish-wpf\Y-popup.exe", `
    "publish-wpf-framework\Y-popup.exe", `
    "docs\Y-popup.exe", `
    "docs\Y-popup-net8.exe", `
    "docs\Y-popup-wpf.exe", `
    "docs\Y-popup-wpf-net8.exe" |
    Format-Table @{N='Path';E={$_.FullName}}, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime
