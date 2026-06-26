# BRUTAL Uninstaller - Build, Publish & Run
Stop-Process -Name "BrutalUninstaller.App" -Force -ErrorAction SilentlyContinue
Remove-Item "$PSScriptRoot\publish" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "[BUILD]" -ForegroundColor Cyan
dotnet build "$PSScriptRoot\src\BrutalUninstaller.App" -c Release
if ($LASTEXITCODE -eq 0) {
    Write-Host "[PUBLISH]" -ForegroundColor Cyan
    dotnet publish "$PSScriptRoot\src\BrutalUninstaller.App" -c Release -o "$PSScriptRoot\publish"
    Write-Host "[RUN]" -ForegroundColor Green
    Start-Process "$PSScriptRoot\publish\BrutalUninstaller.App.exe" -Verb RunAs
} else {
    Write-Host "[FAILED] Build errors - check output above" -ForegroundColor Red
    Read-Host "Press Enter to exit"
}
