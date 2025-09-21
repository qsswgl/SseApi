# 部署指南：SSE API with SSL Auto-Management

## 快速开始

### 1. 设置环境变量

在 PowerShell 中设置 DNSPOD API 凭据：

```powershell
# 设置您的 DNSPOD API 凭据
$env:DP_Id = "594534"
$env:DP_Key = "a30b94f683079f0e36131c2653c77160"

# 验证设置
echo "DP_Id: $env:DP_Id"
echo "DP_Key: $env:DP_Key"
```

### 2. 启动应用程序

```powershell
# 使用启动脚本（推荐）
.\start.ps1

# 或直接运行
dotnet run
```

### 3. 访问测试页面

应用程序启动后：
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001 (开发证书)

一旦 SSL 证书申请成功，您的域名将支持 HTTPS 访问。

## 生产环境部署

### 1. 系统要求

- **操作系统**: Windows Server 2019+ / Linux
- **.NET 版本**: .NET 10.0 或更高
- **端口权限**: 需要绑定 80 和 443 端口
- **域名**: 确保域名 `qsgl.net` 解析到服务器

### 2. 防火墙配置

```powershell
# Windows 防火墙规则
New-NetFirewallRule -DisplayName "Allow HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
New-NetFirewallRule -DisplayName "Allow HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
```

### 3. 作为 Windows 服务运行

#### 安装 .NET Service 工具

```powershell
dotnet tool install --global dotnet-service
```

#### 发布应用程序

```powershell
dotnet publish -c Release -o ./publish
```

#### 安装为 Windows 服务

```powershell
# 创建服务
sc create "SseApiService" binpath="K:\SseApi_NET10\publish\SseApi.exe" start=auto

# 设置服务描述
sc description "SseApiService" "SSE API with SSL Auto-Management"

# 启动服务
sc start "SseApiService"
```

### 4. 使用 IIS 部署（可选）

#### 安装 ASP.NET Core Hosting Bundle

下载并安装 [.NET Hosting Bundle](https://dotnet.microsoft.com/download/dotnet)

#### IIS 配置

1. 创建新的 IIS 应用程序池
2. 设置 .NET CLR 版本为 "无托管代码"
3. 创建网站，指向发布目录
4. 确保应用程序池账户有足够权限

### 5. 环境变量配置

#### 系统级环境变量

```powershell
# 设置系统环境变量（需要管理员权限）
[System.Environment]::SetEnvironmentVariable("DP_Id", "594534", "Machine")
[System.Environment]::SetEnvironmentVariable("DP_Key", "a30b94f683079f0e36131c2653c77160", "Machine")
```

#### 或使用 appsettings.Production.json

```json
{
  "DnsPod": {
    "ApiId": "594534",
    "ApiToken": "a30b94f683079f0e36131c2653c77160"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "SseApi": "Debug"
    }
  }
}
```

## 监控和维护

### 1. 日志监控

应用程序日志位置：
- 控制台输出（开发环境）
- Windows 事件日志（服务模式）
- 自定义日志文件（可配置）

### 2. 证书状态监控

```powershell
# 检查证书状态
Invoke-RestMethod -Uri "http://localhost/ssl/status" -Method GET
```

### 3. 强制证书续期

```powershell
# 手动触发证书续期
Invoke-RestMethod -Uri "http://localhost/ssl/renew" -Method POST
```

### 4. 健康检查脚本

```powershell
# health-check.ps1
$response = try {
    Invoke-RestMethod -Uri "http://localhost/ssl/status" -TimeoutSec 10
} catch {
    Write-Error "健康检查失败: $_"
    exit 1
}

if ($response.hasCertificate -and $response.isValid) {
    Write-Host "✅ 服务正常运行，证书有效" -ForegroundColor Green
    Write-Host "   证书到期时间: $($response.notAfter)"
    Write-Host "   剩余天数: $($response.daysUntilExpiry)"
} else {
    Write-Warning "⚠️ 证书状态异常"
    exit 1
}
```

## 故障排除

### 常见问题

#### 1. 端口绑定失败

```
错误：Unable to bind to https://localhost:443
```

**解决方案**：
- 检查端口是否被占用：`netstat -an | findstr :443`
- 以管理员权限运行应用程序
- 修改 appsettings.json 中的端口配置

#### 2. DNSPOD API 调用失败

```
错误：DnsPod API request failed with status Unauthorized
```

**解决方案**：
- 验证 API ID 和 API Token 是否正确
- 检查 DNSPOD 账户权限
- 确认域名在 DNSPOD 中管理

#### 3. 证书申请失败

```
错误：Authorization failed for domain with status Invalid
```

**解决方案**：
- 检查域名 DNS 解析是否正确
- 确认 DNSPOD API 有 DNS 记录管理权限
- 检查网络连接到 Let's Encrypt 服务器

#### 4. 证书加载失败

```
错误：Error loading existing certificate
```

**解决方案**：
- 检查证书文件权限
- 验证证书存储目录是否存在
- 确认证书文件未损坏

### 日志分析

#### 启用详细日志

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "SseApi.Services": "Trace"
    }
  }
}
```

#### 关键日志消息

- `Certificate requested successfully` - 证书申请成功
- `Certificate renewal completed` - 证书续期完成
- `DNS TXT record added` - DNS 记录添加成功
- `Authorization failed` - 域名验证失败

## 安全建议

### 1. API 密钥安全

- 使用环境变量而非配置文件存储密钥
- 定期轮换 DNSPOD API 密钥
- 限制 API 密钥权限为最小必需

### 2. 证书安全

- 定期备份证书文件
- 设置适当的文件系统权限
- 监控证书到期时间

### 3. 网络安全

- 配置适当的防火墙规则
- 考虑使用 CDN 和 DDoS 防护
- 启用访问日志记录

## 备份和恢复

### 证书备份

```powershell
# 备份证书目录
Copy-Item -Path "./certificates" -Destination "./backup/certificates-$(Get-Date -Format 'yyyyMMdd')" -Recurse
```

### 配置备份

```powershell
# 备份配置文件
Copy-Item -Path "./appsettings*.json" -Destination "./backup/"
```

## 更新和升级

### 应用程序更新

```powershell
# 1. 停止服务
sc stop "SseApiService"

# 2. 备份当前版本
Copy-Item -Path "./publish" -Destination "./backup/publish-$(Get-Date -Format 'yyyyMMdd')" -Recurse

# 3. 部署新版本
dotnet publish -c Release -o ./publish

# 4. 启动服务
sc start "SseApiService"
```

### .NET 版本升级

更新项目文件中的目标框架版本，然后重新发布。