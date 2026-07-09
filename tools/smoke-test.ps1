# Y-popup smoke test: build → test → structure check
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath "$PSScriptRoot\.."

$failures = 0

function Write-Step {
    param([string]$Text, [string]$Color = 'Cyan')
    Write-Host "`n=== $Text ===" -ForegroundColor $Color
}

function Write-Pass([string]$Text) { Write-Host "  PASS: $Text" -ForegroundColor Green }
function Write-Fail([string]$Text) {
    Write-Host "  FAIL: $Text" -ForegroundColor Red
    $script:failures++
}

# 1. Build
Write-Step 'dotnet build'
dotnet build Ypopup.sln --nologo
if ($LASTEXITCODE -eq 0) { Write-Pass 'Build succeeded (0 errors)' }
else { Write-Fail "Build failed (exit $LASTEXITCODE)" }

# 2. Test
Write-Step 'dotnet test'
dotnet test Ypopup.sln --nologo
if ($LASTEXITCODE -eq 0) { Write-Pass 'All tests passed' }
else { Write-Fail "Tests failed (exit $LASTEXITCODE)" }

# 3. Verify project structure
Write-Step 'Project structure'
$checks = @(
    @{ Path = 'Ypopup.sln'; Desc = 'Solution file' },
    @{ Path = 'src\Ypopup.Core\Ypopup.Core.csproj'; Desc = 'Core project' },
    @{ Path = 'src\Ypopup.Network\Ypopup.Network.csproj'; Desc = 'Network project' },
    @{ Path = 'src\Ypopup.Desktop\Ypopup.Desktop.csproj'; Desc = 'Desktop project' },
    @{ Path = 'tests\Ypopup.Core.Tests\Ypopup.Core.Tests.csproj'; Desc = 'Core tests' },
    @{ Path = 'tests\Ypopup.Network.Tests\Ypopup.Network.Tests.csproj'; Desc = 'Network tests' },
    @{ Path = 'docs\index.html'; Desc = 'GitHub Pages' },
    @{ Path = 'publish.ps1'; Desc = 'Publish script' },
    @{ Path = 'push-github.ps1'; Desc = 'Push script' },
    @{ Path = 'tools\create-release.ps1'; Desc = 'Release script' }
)

foreach ($check in $checks) {
    if (Test-Path -LiteralPath $check.Path) { Write-Pass $check.Desc }
    else { Write-Fail "$($check.Desc) not found: $($check.Path)" }
}

# Summary
Write-Step 'Result' $(if ($failures -eq 0) { 'Green' } else { 'Red' })
if ($failures -eq 0) {
    Write-Host "All checks passed. Y-popup is ready for release." -ForegroundColor Green
}
else {
    Write-Host "$failures failure(s) found. Review above." -ForegroundColor Red
    exit 1
}
