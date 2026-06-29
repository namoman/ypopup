# ref/icon.png -> Assets/icon.png, tray.ico, app.ico
dotnet run --project "$PSScriptRoot\Ypopup.IconGenerator\Ypopup.IconGenerator.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
