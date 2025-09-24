param(
    [string]$OutputDir = "K:\SSEAPI",
    [string]$Runtime = "win-x64",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing self-contained service to $OutputDir ..." -ForegroundColor Cyan

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Build publish arguments
$publishArgs = @(
    "publish", "..\SseApi.csproj",
    "-c", "Release",
    "-r", $Runtime,
    "--self-contained", "true",
    "/p:PublishSingleFile=true",
    "/p:IncludeAllContentForSelfExtract=true",
    "/p:PublishReadyToRun=false",
    "-o", $OutputDir
)
if ($NoRestore) { $publishArgs += "--no-restore" }

# Run publish from scripts directory
Push-Location $PSScriptRoot
try {
    dotnet @publishArgs
}
finally {
    Pop-Location
}

Write-Host "Publish complete: $OutputDir" -ForegroundColor Green

# Suggest firewall rules (TCP 5000/5001 and UDP 5001 for HTTP/3)
Write-Host "Tip: open firewall for HTTP/HTTPS and HTTP/3:" -ForegroundColor Yellow
Write-Host "  netsh advfirewall firewall add rule name=\"SseApi_HTTP_5000\" dir=in action=allow protocol=TCP localport=5000" -ForegroundColor DarkYellow
Write-Host "  netsh advfirewall firewall add rule name=\"SseApi_HTTPS_5001\" dir=in action=allow protocol=TCP localport=5001" -ForegroundColor DarkYellow
Write-Host "  netsh advfirewall firewall add rule name=\"SseApi_H3_UDP_5001\" dir=in action=allow protocol=UDP localport=5001" -ForegroundColor DarkYellow
