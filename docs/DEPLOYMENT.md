# SseApi 独立部署指南

## 一键发布到 C:\SseApi（Windows x64，自包含）

使用脚本 `scripts/publish-standalone.ps1`：

```powershell
# 在项目根目录执行
powershell -ExecutionPolicy Bypass -File .\scripts\publish-standalone.ps1
```

默认会：
- 使用 Release 配置、自包含 runtime（win-x64）发布到 `C:\SseApi`
- 将 `wwwroot/` 静态文件包含在内
- 将 `certificates/` 目录（若存在）包含在内

## 证书放置
- 目录：`C:\SseApi\certificates`
- 文件：
  - `qsgl.net.pfx`（包含 `*.qsgl.net` 域名）
  - `qsgl.net.password`（可选，纯文本密码，一行）

## 启动
```powershell
cd C:\SseApi
.\nSseApi.exe
```

启动后监听：
- HTTP: `http://0.0.0.0:5000`
- HTTPS: `https://0.0.0.0:5001`

## 访问测试
- HTTP：`http://localhost:5000/sse-test-page.html`
- HTTPS：`https://3950.qsgl.net:5001/sse-test-page.html`

若需用 IP 访问 HTTPS（例如内网），证书需包含该 IP 的 SAN；否则浏览器会提示不安全或拒绝。

## 防火墙放行
```powershell
New-NetFirewallRule -DisplayName "SseApi HTTP 5000" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "SseApi HTTPS 5001" -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow
```

## 批量发送（多 UsersID）
- 页面：`sse-send.html` 的 UsersID 输入框支持逗号分隔：`1,2,4,5,7`
- API：`POST /sse/UsersID/{UsersID,逗号分隔}/send`
- 返回：`{ success, targets, delivered, perUsers }`

## 生产建议
- 建议前置 Nginx/IIS/Traefik 终止 TLS，仅让后端监听 HTTP（5000）
- 将证书保管与续签交给反向代理，后端服务简化部署
