param(
    [string]$ServiceName = "SseApiService"
)

$ErrorActionPreference = "Stop"

# Require admin
$IsAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Error "请以管理员权限运行此脚本。右键以管理员身份运行 PowerShell。"
    exit 1
}

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') {
        Write-Host "Stopping service $ServiceName ..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    Write-Host "Deleting service $ServiceName ..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Write-Host "服务已删除。" -ForegroundColor Green
} else {
    Write-Host "服务不存在：$ServiceName" -ForegroundColor Yellow
}
