# Y-popup publish: clean → build → release/
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src/Ypopup.Desktop/Ypopup.Desktop.csproj'

$csprojText = Get-Content -LiteralPath $project -Raw
if ($csprojText -notmatch '<Version>([^<]+)</Version>') {
    throw "csproj에서 Version을 찾지 못했습니다: $project"
}
$appVersion = $Matches[1].Trim()

# 다운로드 파일명 끝에 버전. 앱 번들/프로세스 이름(Y-popup)은 유지.
$artifact = @{
    WinExe        = "Y-popup-$appVersion.exe"
    WinNet8       = "Y-popup-net8-$appVersion.exe"
    WinNet8Zip    = "Y-popup-win-x64-net8-$appVersion.zip"
    OsxArm64      = "Y-popup-osx-arm64-$appVersion"
    OsxArm64Net8  = "Y-popup-osx-arm64-net8-$appVersion"
    OsxX64        = "Y-popup-osx-x64-$appVersion"
    OsxX64Net8    = "Y-popup-osx-x64-net8-$appVersion"
}

# OS detection — $IsMacOS available in PowerShell Core 6+ (pwsh on macOS)
$isMacOS = $IsMacOS -eq $true
$macExt = if ($isMacOS) { 'dmg' } else { 'zip' }

# --- Release file list (OS-dependent paths) ---
if ($isMacOS) {
    $releaseFiles = @(
        "release/$($artifact.OsxArm64).dmg",
        "release/$($artifact.OsxArm64Net8).dmg",
        "release/$($artifact.OsxX64).dmg",
        "release/$($artifact.OsxX64Net8).dmg"
    )
}
else {
    $releaseFiles = @(
        "release\$($artifact.WinExe)",
        "release\$($artifact.WinNet8)",
        "release\$($artifact.WinNet8Zip)",
        "release\$($artifact.OsxArm64).zip",
        "release\$($artifact.OsxArm64Net8).zip",
        "release\$($artifact.OsxX64).zip",
        "release\$($artifact.OsxX64Net8).zip"
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

function Compress-ReleaseArchive {
    param(
        [string]$SourcePath,
        [string]$ZipPath
    )

    $attempts = 3
    for ($i = 1; $i -le $attempts; $i++) {
        try {
            if (Test-Path $ZipPath) {
                Remove-Item -Force $ZipPath
            }

            Compress-Archive -Path $SourcePath -DestinationPath $ZipPath -Force
            return
        }
        catch {
            if ($i -eq $attempts) { throw }
            Write-Host "  zip retry $i : $($_.Exception.Message)" -ForegroundColor Yellow
            Start-Sleep -Seconds 2
        }
    }
}

function New-DocsZip {
    param(
        [string]$SourceFolder,
        [string]$ZipPath
    )

    Compress-ReleaseArchive -SourcePath (Join-Path $SourceFolder '*') -ZipPath $ZipPath
}

function Update-DownloadPage {
    param([string]$Version)

    $htmlPath = Join-Path $PSScriptRoot 'docs\index.html'
    if (-not (Test-Path $htmlPath)) { return }

    $html = Get-Content -LiteralPath $htmlPath -Raw -Encoding UTF8
    $replacements = @(
        @{ Pattern = 'Y-popup-osx-arm64-net8(?:-\d+\.\d+\.\d+)?\.(?:zip|dmg)'; Name = "Y-popup-osx-arm64-net8-$Version.zip" }
        @{ Pattern = 'Y-popup-osx-x64-net8(?:-\d+\.\d+\.\d+)?\.(?:zip|dmg)';   Name = "Y-popup-osx-x64-net8-$Version.zip" }
        @{ Pattern = 'Y-popup-osx-arm64(?:-\d+\.\d+\.\d+)?\.(?:zip|dmg)';      Name = "Y-popup-osx-arm64-$Version.zip" }
        @{ Pattern = 'Y-popup-osx-x64(?:-\d+\.\d+\.\d+)?\.(?:zip|dmg)';        Name = "Y-popup-osx-x64-$Version.zip" }
        @{ Pattern = 'Y-popup-win-x64-net8(?:-\d+\.\d+\.\d+)?\.zip';           Name = "Y-popup-win-x64-net8-$Version.zip" }
        @{ Pattern = 'Y-popup-net8(?:-\d+\.\d+\.\d+)?\.exe';                   Name = "Y-popup-net8-$Version.exe" }
        @{ Pattern = 'Y-popup(?:-\d+\.\d+\.\d+)?\.exe';                        Name = "Y-popup-$Version.exe" }
    )

    foreach ($item in $replacements) {
        $html = [regex]::Replace($html, $item.Pattern, [System.Text.RegularExpressions.MatchEvaluator]{ $item.Name })
    }

    $versionLabel = [char]0xBC84 + [char]0xC804
    $html = [regex]::Replace($html, '\uBC84\uC804 \d+\.\d+\.\d+', "$versionLabel $Version")
    $html = [regex]::Replace($html, 'v\d+\.\d+\.\d+', "v$Version")

    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Resolve-Path $htmlPath), $html, $utf8)
    Write-Host "  docs/index.html download names -> $Version"
}

function New-MacosAppBundle {
    param(
        [string]$SourceFolder,
        [string]$AppPath,
        [string]$Version = '2.2.2'
    )

    Remove-PathIfExists $AppPath

    $contentsDir = Join-Path $AppPath 'Contents'
    $macOSDir = Join-Path $contentsDir 'MacOS'
    $resDir = Join-Path $contentsDir 'Resources'
    New-Item -ItemType Directory -Path $macOSDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resDir -Force | Out-Null

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
    Set-Content -Path (Join-Path $contentsDir 'Info.plist') -Value $plist -Encoding UTF8
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
    Copy-Item 'publish\Y-popup.exe' "release\$($artifact.WinExe)" -Force
    New-DocsZip -SourceFolder 'publish-framework' -ZipPath "release\$($artifact.WinNet8Zip)"
    Copy-Item 'publish-framework\Y-popup.exe' "release\$($artifact.WinNet8)" -Force
}

# --- macOS packaging ---
$stagingDir = Join-Path $PSScriptRoot '_staging'
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

$macOSVariants = @(
    @{ Source = 'publish-osx-arm64';            DmgName = "$($artifact.OsxArm64).$macExt" }
    @{ Source = 'publish-osx-arm64-framework';  DmgName = "$($artifact.OsxArm64Net8).$macExt" }
    @{ Source = 'publish-osx-x64';              DmgName = "$($artifact.OsxX64).$macExt" }
    @{ Source = 'publish-osx-x64-framework';    DmgName = "$($artifact.OsxX64Net8).$macExt" }
)

foreach ($variant in $macOSVariants) {
    $appPath = Join-Path $stagingDir 'Y-popup.app'
    Remove-PathIfExists $appPath

    New-MacosAppBundle -SourceFolder $variant.Source -AppPath $appPath -Version $appVersion

    if ($isMacOS) {
        $dmgPath = Join-Path (Join-Path $PSScriptRoot 'release') $variant.DmgName
        New-DmgFromApp -AppPath $appPath -OutputPath $dmgPath -VolumeName 'Y-popup'
    }
    else {
        # Cross-compile from Windows: zip .app (hdiutil unavailable)
        $zipName = $variant.DmgName -replace '\.dmg$', '.zip'
        $zipPath = Join-Path (Join-Path $PSScriptRoot 'release') $zipName
        Compress-ReleaseArchive -SourcePath $appPath -ZipPath $zipPath
        Write-Host "  created zip: $zipPath"
    }

    Remove-PathIfExists $appPath
}

# Clean up staging
Remove-PathIfExists $stagingDir

# --- Package size table ---
Write-Host ""
Write-Host "=== Package sizes ===" -ForegroundColor Green

if ($isMacOS) {
    $packages = @(
        @{ Label = 'macOS arm64 standalone'; Path = "release/$($artifact.OsxArm64).dmg" }
        @{ Label = 'macOS arm64 net8';       Path = "release/$($artifact.OsxArm64Net8).dmg" }
        @{ Label = 'macOS Intel standalone'; Path = "release/$($artifact.OsxX64).dmg" }
        @{ Label = 'macOS Intel net8';       Path = "release/$($artifact.OsxX64Net8).dmg" }
    )
}
else {
    $packages = @(
        @{ Label = 'Windows 64-bit standalone';   Path = "release\$($artifact.WinExe)" }
        @{ Label = 'Windows 64-bit net8 exe';     Path = "release\$($artifact.WinNet8)" }
        @{ Label = 'Windows 64-bit net8 zip';     Path = "release\$($artifact.WinNet8Zip)" }
        @{ Label = 'macOS arm64 standalone zip';  Path = "release\$($artifact.OsxArm64).zip" }
        @{ Label = 'macOS arm64 net8 zip';        Path = "release\$($artifact.OsxArm64Net8).zip" }
        @{ Label = 'macOS Intel standalone zip';  Path = "release\$($artifact.OsxX64).zip" }
        @{ Label = 'macOS Intel net8 zip';        Path = "release\$($artifact.OsxX64Net8).zip" }
    )
}

$packages | ForEach-Object {
    [PSCustomObject]@{
        Package = $_.Label
        Size    = Format-Mb $_.Path
        Path    = (Resolve-Path $_.Path -ErrorAction SilentlyContinue)
    }
} | Format-Table -AutoSize

if (-not $isMacOS) {
    Update-DownloadPage -Version $appVersion
}

Write-Host ""
Write-Host "=== 릴리스 파일이 release/ 폴더에 생성되었습니다 ($appVersion) ===" -ForegroundColor Green
Write-Host "GitHub Releases 업로드: gh release create v$appVersion release/* --title `"v$appVersion`"" -ForegroundColor Yellow
