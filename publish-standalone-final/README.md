# SseApi 独立运行版本 - 最终版

## 部署说明

这是一个独立运行的 SseApi 版本，包含了所有必要的 .NET 运行时文件，可以在没有安装 .NET 的 Windows x64 机器上直接运行。

## 🚀 快速开始

### 方法1：使用启动脚本（推荐）
双击 `start-standalone.bat`

### 方法2：直接运行
双击 `SseApi.exe` 或在命令行中执行：
```
SseApi.exe
```

## 📊 访问地址
- **HTTP**: http://localhost:5000
- **HTTPS**: https://localhost:5001  
- **主页**: http://localhost:5000/ (显示所有可用页面)

## 🧪 测试页面
- **完整测试页面**: http://localhost:5000/sse-test-page.html
- **发送页面**: http://localhost:5000/sse-send.html  
- **接收页面**: http://localhost:5000/sse-recv.html

## 🔌 API端点
- `GET /sse/UsersID/{用户ID}` - SSE连接
- `POST /sse/UsersID/{用户ID}/send` - 发送消息

## ⚙️ 配置说明
修改 `appsettings.json` 可以调整：
- 端口配置 (默认5000/5001)
- SSL证书设置
- DNSPod API 配置

## ✨ 功能特性
- ✅ SSE (Server-Sent Events) 实时消息推送
- ✅ 自动SSL证书申请和续期
- ✅ DNSPod API 集成
- ✅ IPv4/IPv6 双栈支持
- ✅ 无需安装 .NET 运行时
- ✅ 包含所有依赖项
- ✅ 默认文件支持
- ✅ 修复静态文件访问问题

## 💻 系统要求
- Windows x64 (Windows 10/11, Windows Server 2016+)
- 至少 120MB 磁盘空间

## 📁 文件结构
```
publish-standalone-final/
├── SseApi.exe              # 主程序
├── start-standalone.bat    # 启动脚本
├── README.md              # 说明文档
├── appsettings.json       # 配置文件
├── wwwroot/               # 静态网页文件
│   ├── index.html         # 主页
│   ├── sse-test-page.html # 完整测试页面
│   ├── sse-send.html      # 发送页面
│   └── sse-recv.html      # 接收页面
└── *.dll                  # .NET运行时文件
```

## 🔧 故障排除

### 无法访问网页
1. 确认应用程序已启动
2. 尝试HTTP版本：http://localhost:5000
3. 检查防火墙设置
4. 如果HTTPS有证书警告，点击"继续访问"

### 应用程序无法启动
1. 确认是Windows x64系统
2. 检查是否有杀毒软件阻止
3. 尝试以管理员身份运行

## 📝 更新日志
- **v3 (Final)**: 完全修复静态文件问题，稳定发布版本
- **v2**: 修复静态文件访问问题，添加IPv4支持  
- **v1**: 初始独立运行版本