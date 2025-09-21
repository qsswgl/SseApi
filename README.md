# SseApi - .NET 10 ASP.NET Core Web API

这是一个基于 .NET 10.0 的 ASP.NET Core Web API 项目。

## 项目信息

- **框架版本**: .NET 10.0.100-RC.1.25451.107
- **项目类型**: ASP.NET Core Web API
- **开发环境**: Visual Studio Code

## 依赖包

- Microsoft.AspNetCore.OpenApi (10.0.0-rc.1.*)
- Swashbuckle.AspNetCore (9.0.4)

## 如何运行

### 前提条件

确保你已安装 .NET 10.0 RC 版本：

```bash
dotnet --version
```

应该显示: `10.0.100-rc.1.25451.107`

### 运行项目

1. 克隆项目：
```bash
git clone https://github.com/qsswgl/SseApi.git
cd SseApi
```

2. 恢复依赖包：
```bash
dotnet restore
```

3. 编译项目：
```bash
dotnet build
```

4. 运行项目：
```bash
dotnet run
```

项目将在以下地址启动：
- HTTP: http://localhost:5103
- HTTPS: https://localhost:7143

## 项目结构

```
SseApi/
├── Program.cs                 # 应用程序入口点
├── SseApi.csproj             # 项目文件
├── appsettings.json          # 应用程序配置
├── appsettings.Development.json # 开发环境配置
├── sse-test-page.html        # SSE测试页面
├── SseApi.http              # HTTP请求测试文件
└── Properties/
    └── launchSettings.json   # 启动配置
```

## 功能特性

- 基于 .NET 10 最新功能
- OpenAPI/Swagger 支持
- Server-Sent Events (SSE) 支持
- 热重载开发体验
- 现代化的 ASP.NET Core 架构

## 开发说明

这个项目从 .NET 9.0 升级到 .NET 10.0，包含了最新的 .NET 功能和性能改进。

## 许可证

[请在此处添加你的许可证信息]