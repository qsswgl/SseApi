# SSE API with Automatic SSL Certificate Management

这是一个集成了自动化 SSL 证书申请和管理功能的 ASP.NET Core Web API 项目，支持通过 DNSPOD API 自动申请和续期 Let's Encrypt 泛域名证书。

## 功能特性

### 核心功能
- ✅ Server-Sent Events (SSE) 实时消息推送
- ✅ 自动申请 Let's Encrypt 泛域名证书 (*.qsgl.net)
- ✅ 基于 DNSPOD API 的 DNS-01 验证
- ✅ 自动证书续期（到期前30天自动续期）
- ✅ 动态证书加载，无需重启服务
- ✅ 证书状态监控和管理 API

### SSL 证书管理
- 支持 DNSPOD API 自动 DNS 验证
- 自动化泛域名证书申请 (*.qsgl.net)
- 智能证书续期（可配置续期天数）
- 证书状态实时监控
- 支持手动强制续期

## 环境要求

- .NET 10.0 或更高版本
- DNSPOD 账户和 API 密钥
- 域名：qsgl.net (需要在 DNSPOD 管理)

## 配置说明

### 1. 环境变量配置

在系统环境变量或 `.env` 文件中设置：

```bash
# DNSPOD API 配置
export DP_Id="594534"
export DP_Key="a30b94f683079f0e36131c2653c77160"
```

或者在 PowerShell 中：

```powershell
$env:DP_Id = "594534"
$env:DP_Key = "a30b94f683079f0e36131c2653c77160"
```

### 2. appsettings.json 配置

```json
{
  "SslCertificate": {
    "Domain": "qsgl.net",
    "WildcardDomain": "*.qsgl.net",
    "Email": "admin@qsgl.net",
    "CertificateStorePath": "./certificates",
    "AutoRenewDays": 30,
    "CheckIntervalHours": 6
  },
  "DnsPod": {
    "ApiId": "",
    "ApiToken": ""
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:80"
      },
      "Https": {
        "Url": "https://*:443"
      }
    }
  }
}
```

## 安装和运行

### 1. 克隆项目并安装依赖

```bash
git clone <repository-url>
cd SseApi_NET10
dotnet restore
```

### 2. 配置环境变量

```powershell
# Windows PowerShell
$env:DP_Id = "你的DNSPOD_API_ID"
$env:DP_Key = "你的DNSPOD_API_KEY"
```

### 3. 运行项目

```bash
dotnet run
```

## API 端点

### SSE 相关
- `GET /sse` - 建立 SSE 连接
- `GET /sse/status` - 获取连接状态
- `POST /sse/broadcast` - 广播消息到所有连接
- `POST /sse/send/{clientId}` - 发送消息到特定客户端

### SSL 证书管理
- `GET /ssl/status` - 获取证书状态
- `POST /ssl/renew` - 手动强制续期证书

### 其他
- `GET /` - 测试页面
- `GET /weatherforecast` - 示例天气预报 API

## 证书管理流程

### 自动申请流程

1. **检查现有证书**：启动时检查是否有有效证书
2. **申请新证书**：如果没有证书或即将过期，自动申请
3. **DNS 验证**：通过 DNSPOD API 添加 TXT 记录进行 DNS-01 验证
4. **证书生成**：Let's Encrypt 验证通过后生成证书
5. **证书存储**：将证书保存到本地文件系统
6. **动态加载**：无需重启即可使用新证书

### 自动续期

- 每6小时检查一次证书状态
- 到期前30天自动启动续期流程
- 续期成功后通过 SSE 通知客户端
- 续期失败会记录日志并在下次检查时重试

## 目录结构

```
SseApi_NET10/
├── Services/
│   ├── DnsPodApiClient.cs      # DNSPOD API 客户端
│   ├── AcmeService.cs          # ACME 协议客户端
│   └── CertificateRenewalService.cs  # 证书续期服务
├── certificates/               # 证书存储目录（自动创建）
├── wwwroot/
│   └── sse-test-page.html      # 测试页面
├── Program.cs                  # 主程序
├── appsettings.json           # 配置文件
└── SseApi.csproj              # 项目文件
```

## 安全注意事项

1. **API 密钥安全**：
   - 不要将 DNSPOD API 密钥提交到版本控制
   - 使用环境变量或安全的密钥管理服务
   - 定期轮换 API 密钥

2. **证书存储**：
   - 证书文件存储在 `./certificates` 目录
   - 确保该目录有适当的文件权限
   - 建议定期备份证书文件

3. **网络安全**：
   - 确保服务器防火墙正确配置
   - 仅开放必要的端口（80, 443）
   - 考虑使用反向代理（如 Nginx）

## 故障排除

### 常见问题

1. **DNSPOD API 调用失败**
   - 检查 API ID 和 API Key 是否正确
   - 确认域名在 DNSPOD 账户中
   - 检查 API 密钥权限

2. **证书申请失败**
   - 查看日志了解具体错误信息
   - 确认 DNS 解析是否正常
   - 检查 Let's Encrypt 服务状态

3. **证书加载失败**
   - 检查证书文件权限
   - 确认证书文件未损坏
   - 查看应用程序日志

### 日志查看

应用程序会记录详细的日志信息：

```bash
# 查看实时日志
dotnet run --verbosity normal

# 或查看特定日志级别
dotnet run --configuration Debug
```

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request。

## 联系方式

如有问题，请联系：admin@qsgl.net