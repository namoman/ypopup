# Y-popup publish: clean → build → docs/
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src/Ypopup.Desktop/Ypopup.Desktop.csproj'

$docsDeploymentFiles = @(
    'docs\Y-popup.exe',
    'docs\Y-popup-net8.exe',
    'docs\Y-popup-win-x64-net8.zip',
    'docs\Y-popup-osx-arm64.zip',
    'docs\Y-popup-osx-arm64-net8.zip',
    'docs\Y-popup-osx-x64.zip',
    'docs\Y-popup-osx-x64-net8.zip'
)

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

function Clean-DocsDeployment {
    Write-Host "=== Clean docs deployment files ===" -ForegroundColor Cyan
    foreach ($file in $docsDeploymentFiles) {
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

Clean-RunningApp
Clean-BuildCache
Clean-PublishFolders
Clean-DocsDeployment

Write-Host "=== Regenerate icons ===" -ForegroundColor Cyan
powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\generate-app-icon.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== Windows x64 ===" -ForegroundColor Cyan
Publish-Target -Rid 'win-x64' -Output 'publish' -SelfContained $true -Compress $true
Publish-Target -Rid 'win-x64' -Output 'publish-framework' -SelfContained $false -Compress $false

Write-Host "=== macOS Apple Silicon (arm64) ===" -ForegroundColor Cyan
Publish-Target -Rid 'osx-arm64' -Output 'publish-osx-arm64' -SelfContained $true -Compress $false
Publish-Target -Rid 'osx-arm64' -Output 'publish-osx-arm64-framework' -SelfContained $false -Compress $false

Write-Host "=== macOS Intel (x64) ===" -ForegroundColor Cyan
Publish-Target -Rid 'osx-x64' -Output 'publish-osx-x64' -SelfContained $true -Compress $false
Publish-Target -Rid 'osx-x64' -Output 'publish-osx-x64-framework' -SelfContained $false -Compress $false

Write-Host "=== docs/ copy ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path 'docs' | Out-Null

Copy-Item 'publish\Y-popup.exe' 'docs\Y-popup.exe' -Force
New-DocsZip -SourceFolder 'publish-framework' -ZipPath 'docs\Y-popup-win-x64-net8.zip'
New-DocsZip -SourceFolder 'publish-osx-arm64' -ZipPath 'docs\Y-popup-osx-arm64.zip'
New-DocsZip -SourceFolder 'publish-osx-arm64-framework' -ZipPath 'docs\Y-popup-osx-arm64-net8.zip'
New-DocsZip -SourceFolder 'publish-osx-x64' -ZipPath 'docs\Y-popup-osx-x64.zip'
New-DocsZip -SourceFolder 'publish-osx-x64-framework' -ZipPath 'docs\Y-popup-osx-x64-net8.zip'
Copy-Item 'publish-framework\Y-popup.exe' 'docs\Y-popup-net8.exe' -Force

Write-Host ""
Write-Host "=== Package sizes ===" -ForegroundColor Green
@(
    @{ Label = 'Windows 64-bit standalone'; Path = 'docs\Y-popup.exe' },
    @{ Label = 'Windows 64-bit net8 zip'; Path = 'docs\Y-popup-win-x64-net8.zip' },
    @{ Label = 'macOS arm64 standalone zip'; Path = 'docs\Y-popup-osx-arm64.zip' },
    @{ Label = 'macOS arm64 net8 zip'; Path = 'docs\Y-popup-osx-arm64-net8.zip' },
    @{ Label = 'macOS Intel standalone zip'; Path = 'docs\Y-popup-osx-x64.zip' },
    @{ Label = 'macOS Intel net8 zip'; Path = 'docs\Y-popup-osx-x64-net8.zip' }
) | ForEach-Object {
    [PSCustomObject]@{
        Package = $_.Label
        Size    = Format-Mb $_.Path
        Path    = (Resolve-Path $_.Path -ErrorAction SilentlyContinue)
    }
} | Format-Table -AutoSize
