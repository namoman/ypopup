# Y-popup publish: Avalonia Windows x64 + macOS
Get-Process -Name "Y-popup" -ErrorAction SilentlyContinue | Stop-Process -Force

powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\generate-app-icon.ps1"

$project = 'src/Ypopup.Desktop/Ypopup.Desktop.csproj'

function Publish-Target {
    param(
        [string]$Rid,
        [string]$Output,
        [bool]$SelfContained,
        [bool]$Compress
    )

    if (Test-Path $Output) {
        Remove-Item -Recurse -Force $Output
    }

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
