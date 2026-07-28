# Y-popup publish: clean → build → release/
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src/Ypopup.Desktop/Ypopup.Desktop.csproj'

# OS detection — $IsMacOS available in PowerShell Core 6+ (pwsh on macOS)
$isMacOS = $IsMacOS -eq $true

# --- Release file list (OS-dependent paths) ---
if ($isMacOS) {
    $releaseFiles = @(
        'release/Y-popup-osx-arm64.dmg',
        'release/Y-popup-osx-arm64-net8.dmg',
        'release/Y-popup-osx-x64.dmg',
        'release/Y-popup-osx-x64-net8.dmg'
    )
}
else {
    $releaseFiles = @(
        'release\Y-popup.exe',
        'release\Y-popup-net8.exe',
        'release\Y-popup-win-x64-net8.zip',
        'release\Y-popup-osx-arm64.zip',
        'release\Y-popup-osx-arm64-net8.zip',
        'release\Y-popup-osx-x64.zip',
        'release\Y-popup-osx-x64-net8.zip'
    )
}

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Recurse -Force $Path
        Write-Host "  removed $Path"
    }
}

function Clean-RunningApp {
    Write-Host "=== Stop Y-popup ===" -ForegroundColor Cyan
    Get-Process -Name 'Y-popup' -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Clean-BuildCache {
    Write-Host "=== Clean bin/obj ===" -ForegroundColor Cyan
    foreach ($root in @('src', 'tools')) {
        if (-not (Test-Path $root)) { continue }

        Get-ChildItem -Path $root -Recurse -Directory -Filter bin -ErrorAction SilentlyContinue |
            ForEach-Object { Remove-PathIfExists $_.FullName }
        Get-ChildItem -Path $root -Recurse -Directory -Filter obj -ErrorAction SilentlyContinue |
            ForEach-Object { Remove-PathIfExists $_.FullName }
    }
}

function Clean-PublishFolders {
    Write-Host "=== Clean publish* folders ===" -ForegroundColor Cyan
    Get-ChildItem -Path . -Directory -Filter 'publish*' -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-PathIfExists $_.FullName }
}

function Clean-ReleaseFiles {
    Write-Host "=== Clean release files ===" -ForegroundColor Cyan
    foreach ($file in $releaseFiles) {
        if (Test-Path $file) {
            Remove-Item -Force $file
            Write-Host "  removed $file"
        }
    }
}

function Publish-Target {
    param(
        [string]$Rid,
        [string]$Output,
        [bool]$SelfContained,
        [bool]$Compress
    )

    Remove-PathIfExists $Output

    $args = @(
        'publish', $project,
        '-c', 'Release',
        '-r', $Rid,
        '-o', $Output,
        '/p:PublishSingleFile=true',
        '/p:DebugType=None',
        '/p:DebugSymbols=false',
        "/p:SelfContained=$SelfContained",
        "/p:IncludeNativeLibrariesForSelfExtract=$SelfContained",
        "/p:EnableCompressionInSingleFile=$Compress"
    )
    dotnet @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function New-DocsZip {
    param(
        [string]$SourceFolder,
        [string]$ZipPath
    )

    if (Test-Path $ZipPath) {
        Remove-Item -Force $ZipPath
    }

    Compress-Archive -Path (Join-Path $SourceFolder '*') -DestinationPath $ZipPath -Force
}

function New-MacosAppBundle {
    param(
        [string]$SourceFolder,
        [string]$AppPath,
        [string]$Version = '2.1.0'
    )

    Remove-PathIfExists $AppPath

    $macOSDir = Join-Path $AppPath 'Contents' 'MacOS'
    $resDir   = Join-Path $AppPath 'Contents' 'Resources'
    New-Item -ItemType Directory -Path $macOSDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resDir   -Force | Out-Null

    # Copy publish output (single-file executable + any extras) into MacOS/
    Get-ChildItem -Path $SourceFolder | Copy-Item -Destination $macOSDir -Recurse -Force

    # Create Info.plist
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>Y-popup</string>
    <key>CFBundleIdentifier</key>
    <string>com.namoman.ypopup</string>
    <key>CFBundleName</key>
    <string>Y-popup</string>
    <key>CFBundleDisplayName</key>
    <string>Y-popup</string>
    <key>CFBundleVersion</key>
    <string>$Version</string>
    <key>CFBundleShortVersionString</key>
    <string>$Version</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"@
    Set-Content -Path (Join-Path $AppPath 'Contents' 'Info.plist') -Value $plist -Encoding UTF8
    Write-Host "  created .app bundle: $AppPath"
}

function New-DmgFromApp {
    param(
        [string]$AppPath,
        [string]$OutputPath,
        [string]$VolumeName
    )

    if (-not (Get-Command 'hdiutil' -ErrorAction SilentlyContinue)) {
        Write-Host "  WARNING: hdiutil not found, creating zip instead" -ForegroundColor Yellow
        $zipPath = $OutputPath -replace '\.dmg$', '.zip'
        New-DocsZip -SourceFolder $AppPath -ZipPath $zipPath
        return
    }

    if (Test-Path $OutputPath) {
        Remove-Item -Force $OutputPath
    }

    Write-Host "  creating DMG: $OutputPath"
    & hdiutil create -volname $VolumeName -srcfolder $AppPath -ov -format UDZO $OutputPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Format-Mb {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return 'n/a' }
    $item = Get-Item $Path
    if ($item.PSIsContainer) {
        $bytes = (Get-ChildItem $Path -File -Recurse | Measure-Object -Property Length -Sum).Sum
    }
    else {
        $bytes = $item.Length
    }
    return "{0:N1} MB" -f ($bytes / 1MB)
}

# ============================================================
# MAIN
# ============================================================

Clean-RunningApp
Clean-BuildCache
Clean-PublishFolders
Clean-ReleaseFiles

# --- Icon (Windows only) ---
if ($isMacOS) {
    Write-Host "=== icon generation skipped (macOS) ===" -ForegroundColor DarkYellow
}
else {
    Write-Host "=== Regenerate icons ===" -ForegroundColor Cyan
    powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\generate-app-icon.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# --- Windows publish ---
if (-not $isMacOS) {
    Write-Host "=== Windows x64 ===" -ForegroundColor Cyan
    Publish-Target -Rid 'win-x64' -Output 'publish' -SelfContained $true -Compress $true
    Publish-Target -Rid 'win-x64' -Output 'publish-framework' -SelfContained $false -Compress $false
}

# --- macOS publish ---
Write-Host "=== macOS Apple Silicon (arm64) ===" -ForegroundColor Cyan
Publish-Target -Rid 'osx-arm64' -Output 'publish-osx-arm64' -SelfContained $true -Compress $false
Publish-Target -Rid 'osx-arm64' -Output 'publish-osx-arm64-framework' -SelfContained $false -Compress $false

Write-Host "=== macOS Intel (x64) ===" -ForegroundColor Cyan
Publish-Target -Rid 'osx-x64' -Output 'publish-osx-x64' -SelfContained $true -Compress $false
Publish-Target -Rid 'osx-x64' -Output 'publish-osx-x64-framework' -SelfContained $false -Compress $false

# --- release/ directory ---
New-Item -ItemType Directory -Force -Path 'release' | Out-Null

# --- Windows release files ---
if (-not $isMacOS) {
    Copy-Item 'publish\Y-popup.exe' 'release\Y-popup.exe' -Force
    New-DocsZip -SourceFolder 'publish-framework' -ZipPath 'release\Y-popup-win-x64-net8.zip'
    Copy-Item 'publish-framework\Y-popup.exe' 'release\Y-popup-net8.exe' -Force
}

# --- macOS packaging ---
$stagingDir = Join-Path $PSScriptRoot '_staging'
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

$macOSVariants = @(
    @{ Source = 'publish-osx-arm64';            DmgName = 'Y-popup-osx-arm64.dmg' }
    @{ Source = 'publish-osx-arm64-framework';  DmgName = 'Y-popup-osx-arm64-net8.dmg' }
    @{ Source = 'publish-osx-x64';              DmgName = 'Y-popup-osx-x64.dmg' }
    @{ Source = 'publish-osx-x64-framework';    DmgName = 'Y-popup-osx-x64-net8.dmg' }
)

foreach ($variant in $macOSVariants) {
    $appPath = Join-Path $stagingDir 'Y-popup.app'
    Remove-PathIfExists $appPath

    if ($isMacOS) {
        New-MacosAppBundle -SourceFolder $variant.Source -AppPath $appPath
        $dmgPath = Join-Path (Join-Path $PSScriptRoot 'release') $variant.DmgName
        New-DmgFromApp -AppPath $appPath -OutputPath $dmgPath -VolumeName 'Y-popup'
        Remove-PathIfExists $appPath
    }
    else {
        # Cross-compile from Windows: create zip (hdiutil unavailable)
        $zipName = $variant.DmgName -replace '\.dmg$', '.zip'
        New-DocsZip -SourceFolder $variant.Source -ZipPath (Join-Path (Join-Path $PSScriptRoot 'release') $zipName)
    }
}

# Clean up staging
Remove-PathIfExists $stagingDir

# --- Package size table ---
Write-Host ""
Write-Host "=== Package sizes ===" -ForegroundColor Green

if ($isMacOS) {
    $packages = @(
        @{ Label = 'macOS arm64 standalone'; Path = 'release/Y-popup-osx-arm64.dmg' }
        @{ Label = 'macOS arm64 net8';       Path = 'release/Y-popup-osx-arm64-net8.dmg' }
        @{ Label = 'macOS Intel standalone'; Path = 'release/Y-popup-osx-x64.dmg' }
        @{ Label = 'macOS Intel net8';       Path = 'release/Y-popup-osx-x64-net8.dmg' }
    )
}
else {
    $packages = @(
        @{ Label = 'Windows 64-bit standalone';   Path = 'release\Y-popup.exe' }
        @{ Label = 'Windows 64-bit net8 zip';     Path = 'release\Y-popup-win-x64-net8.zip' }
        @{ Label = 'macOS arm64 standalone zip';  Path = 'release\Y-popup-osx-arm64.zip' }
        @{ Label = 'macOS arm64 net8 zip';        Path = 'release\Y-popup-osx-arm64-net8.zip' }
        @{ Label = 'macOS Intel standalone zip';  Path = 'release\Y-popup-osx-x64.zip' }
        @{ Label = 'macOS Intel net8 zip';        Path = 'release\Y-popup-osx-x64-net8.zip' }
    )
}

$packages | ForEach-Object {
    [PSCustomObject]@{
        Package = $_.Label
        Size    = Format-Mb $_.Path
        Path    = (Resolve-Path $_.Path -ErrorAction SilentlyContinue)
    }
} | Format-Table -AutoSize

Write-Host ""
Write-Host "=== 릴리스 파일이 release/ 폴더에 생성되었습니다 ===" -ForegroundColor Green
Write-Host "GitHub Releases 업로드: gh release create v2.x.x release/* --title \"v2.x.x\"" -ForegroundColor Yellow
