# Y-popup: publish → git commit → GitHub push
param(
    [string]$Message = '',
    [switch]$SkipPublish,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Git {
    param([string[]]$GitArgs)

    if ($DryRun) {
        Write-Host "[dry-run] git $($GitArgs -join ' ')" -ForegroundColor DarkGray
        return
    }

    & git @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs[0]) failed (exit $LASTEXITCODE)"
    }
}

if (-not $SkipPublish) {
    Write-Host "=== publish.ps1 ===" -ForegroundColor Cyan
    & "$PSScriptRoot\publish.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else {
    Write-Host "=== Skip publish (SkipPublish) ===" -ForegroundColor Yellow
}

Write-Host "=== Git status ===" -ForegroundColor Cyan
Invoke-Git @('status', '--short')

Write-Host "=== Git add ===" -ForegroundColor Cyan
Invoke-Git @('add', 'README.md', 'walkthrough.md', 'publish.ps1', 'push-github.ps1', '.gitignore')
Invoke-Git @('add', 'src', 'tools', 'docs\index.html', 'docs\screenshot.png', 'docs\cross-platform-support.md')

# 로컬 테스트용 docs/share 는 제외
if (-not $DryRun) {
    git reset -- docs/share 2>$null | Out-Null
}

$staged = git diff --cached --name-only
if ([string]::IsNullOrWhiteSpace($staged)) {
    Write-Host "커밋할 변경 사항이 없습니다." -ForegroundColor Yellow
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = "update"
}

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
Write-Host "=== Git commit ($branch) ===" -ForegroundColor Cyan
Write-Host "Message: $Message"
Invoke-Git @('commit', '-m', $Message)

Write-Host "=== Git push origin/$branch ===" -ForegroundColor Cyan
Invoke-Git @('push', 'origin', $branch)

Write-Host ""
Write-Host "완료: https://github.com/namoman/ypopup" -ForegroundColor Green
Write-Host "Pages: https://namoman.github.io/ypopup/" -ForegroundColor Green
