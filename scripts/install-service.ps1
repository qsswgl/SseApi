param(
    [string]$ServiceName = "SseApiService",
    [string]$DisplayName = "Sse API Service",
    [string]$Description = "Server-Sent Events API (HTTP/1.1 + HTTP/2 + HTTP/3)",
    [string]$BinaryPath = "K:\SSEAPI\SseApi.exe"
)

$ErrorActionPreference = "Stop"

# Require admin
$IsAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Error "请以管理员权限运行此脚本。右键以管理员身份运行 PowerShell。"
    exit 1
}

# Stop and delete existing service if present
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') {
        Write-Host "Stopping existing service $ServiceName ..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    Write-Host "Deleting existing service $ServiceName ..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create service (local service account)
Write-Host "Creating service $ServiceName ..." -ForegroundColor Cyan
$bin = '"' + $BinaryPath + '"'
sc.exe create $ServiceName binPath= $bin start= auto DisplayName= "$DisplayName" | Out-Null
sc.exe description $ServiceName "$Description" | Out-Null

# Set recovery options (restart on failure)
# 1st failure: restart after 5s; 2nd: restart after 5s; subsequent: restart after 5s
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

# Open firewall rules for ports
Write-Host "Ensuring firewall rules exist ..." -ForegroundColor Cyan
netsh advfirewall firewall add rule name="SseApi_HTTP_5000" dir=in action=allow protocol=TCP localport=5000 | Out-Null
netsh advfirewall firewall add rule name="SseApi_HTTPS_5001" dir=in action=allow protocol=TCP localport=5001 | Out-Null
netsh advfirewall firewall add rule name="SseApi_H3_UDP_5001" dir=in action=allow protocol=UDP localport=5001 | Out-Null

# Start service
Write-Host "Starting service $ServiceName ..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

Write-Host "安装完成。使用 'Get-Service $ServiceName' 查看状态。" -ForegroundColor Green
