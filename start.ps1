#!/usr/bin/env pwsh

# SSL Certificate Auto-Management Startup Script
# 自动化 SSL 证书管理启动脚本

Write-Host "🚀 正在启动 SSE API with SSL Auto-Management..." -ForegroundColor Green

# 检查环境变量
Write-Host "📋 检查环境配置..." -ForegroundColor Yellow

if (-not $env:DP_Id) {
    Write-Host "⚠️  环境变量 DP_Id 未设置，请设置您的 DNSPOD API ID" -ForegroundColor Red
    Write-Host "   示例: `$env:DP_Id = '594534'" -ForegroundColor Gray
}

if (-not $env:DP_Key) {
    Write-Host "⚠️  环境变量 DP_Key 未设置，请设置您的 DNSPOD API Key" -ForegroundColor Red
    Write-Host "   示例: `$env:DP_Key = 'your-api-key'" -ForegroundColor Gray
}

if ($env:DP_Id -and $env:DP_Key) {
    Write-Host "✅ DNSPOD API 配置已设置" -ForegroundColor Green
    Write-Host "   API ID: $env:DP_Id" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "🔧 如需设置环境变量，请运行以下命令:" -ForegroundColor Cyan
    Write-Host "   `$env:DP_Id = '你的DNSPOD_API_ID'" -ForegroundColor White
    Write-Host "   `$env:DP_Key = '你的DNSPOD_API_KEY'" -ForegroundColor White
    Write-Host ""
    Write-Host "🌐 或者在 appsettings.json 中配置 DnsPod 节点" -ForegroundColor Cyan
    Write-Host ""
}

# 检查证书目录
$certDir = "./certificates"
if (-not (Test-Path $certDir)) {
    Write-Host "📁 创建证书存储目录: $certDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $certDir -Force | Out-Null
}

# 显示配置信息
Write-Host ""
Write-Host "⚙️  当前配置:" -ForegroundColor Cyan
Write-Host "   域名: qsgl.net (泛域名: *.qsgl.net)" -ForegroundColor Gray
Write-Host "   证书存储: $certDir" -ForegroundColor Gray
Write-Host "   HTTP端口: 80" -ForegroundColor Gray
Write-Host "   HTTPS端口: 443" -ForegroundColor Gray

Write-Host ""
Write-Host "🔒 SSL 证书自动化功能:" -ForegroundColor Cyan
Write-Host "   ✅ 自动申请 Let's Encrypt 泛域名证书" -ForegroundColor Gray
Write-Host "   ✅ 基于 DNSPOD DNS-01 验证" -ForegroundColor Gray
Write-Host "   ✅ 自动续期（到期前30天）" -ForegroundColor Gray
Write-Host "   ✅ 每6小时检查证书状态" -ForegroundColor Gray
Write-Host "   ✅ 实时状态监控" -ForegroundColor Gray

Write-Host ""
Write-Host "🌐 API 端点:" -ForegroundColor Cyan
Write-Host "   GET  /              - 测试页面" -ForegroundColor Gray
Write-Host "   GET  /sse           - SSE 连接" -ForegroundColor Gray
Write-Host "   GET  /sse/status    - 连接状态" -ForegroundColor Gray
Write-Host "   POST /sse/broadcast - 广播消息" -ForegroundColor Gray
Write-Host "   GET  /ssl/status    - 证书状态" -ForegroundColor Gray
Write-Host "   POST /ssl/renew     - 强制续期" -ForegroundColor Gray

Write-Host ""
Write-Host "🏃‍♂️ 启动应用程序..." -ForegroundColor Green

# 启动应用程序
try {
    dotnet run
} catch {
    Write-Host ""
    Write-Host "❌ 应用程序启动失败: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔧 故障排除建议:" -ForegroundColor Yellow
    Write-Host "   1. 检查 .NET 10.0 是否已安装" -ForegroundColor Gray
    Write-Host "   2. 运行 'dotnet restore' 恢复依赖" -ForegroundColor Gray
    Write-Host "   3. 检查端口 80 和 443 是否被占用" -ForegroundColor Gray
    Write-Host "   4. 确认防火墙设置允许这些端口" -ForegroundColor Gray
    exit 1
}