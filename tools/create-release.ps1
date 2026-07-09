# Y-popup: GitHub Release 생성
param(
    [string]$Version = '',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..

if ([string]::IsNullOrWhiteSpace($Version)) {
    # src/Ypopup.Core/AppInfo.cs 에서 버전 읽기
    $appInfo = Get-Content 'src\Ypopup.Core\AppInfo.cs'
    $match = [regex]::Match($appInfo, 'VersionDisplay\s*=\s*"([^"]+)"')
    $Version = if ($match.Success) { $match.Groups[1].Value } else { "v0.0.0" }
}

if (-not (Test-Path 'release')) {
    Write-Host "release/ 폴더가 없습니다. 먼저 publish.ps1을 실행하세요." -ForegroundColor Red
    exit 1
}

$files = Get-ChildItem 'release' -File
if ($files.Count -eq 0) {
    Write-Host "release/ 폴더에 파일이 없습니다. 먼저 publish.ps1을 실행하세요." -ForegroundColor Red
    exit 1
}

$tag = "v$Version"
Write-Host "=== Creating GitHub Release: $tag ===" -ForegroundColor Cyan
Write-Host "Files:"
$files | ForEach-Object { Write-Host "  $($_.Name) ($( '{0:N1} MB' -f ($_.Length / 1MB) ))" }

if ($DryRun) {
    Write-Host "[dry-run] gh release create $tag release/* --title ""$tag""" -ForegroundColor DarkGray
    return
}

$fileList = ($files.FullName -join ' ')
Write-Host "Running: gh release create $tag release/* --title ""$tag""" -ForegroundColor DarkGray
& gh release create $tag $files.FullName --title $tag 2>&1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "릴리스 생성 완료: https://github.com/namoman/ypopup/releases/tag/$tag" -ForegroundColor Green
}
else {
    Write-Host "릴리스 생성 실패 (exit $exitCode)" -ForegroundColor Red
}
