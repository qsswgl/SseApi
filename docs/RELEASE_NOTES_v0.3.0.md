# SseApi v0.3.0

日期: 2025-09-23

## 亮点
- SSE 多 UsersID 同步推送：支持在 `POST /sse/UsersID/{UsersID}/send` 中以逗号分隔多个 ID，一次性群发。
- 独立部署改进：`UseContentRoot(AppContext.BaseDirectory)`；证书文件按相对路径加载；`wwwroot`/默认页启用。
- HTTPS 证书选择：优先使用 `./certificates/qsgl.net.pfx`（可配 `.password`），缺失时回退系统默认选择。
- 发布与版本化：新增发布脚本与标签脚本；完善 `.gitignore` 避免误提交产物与敏感文件。

## 变更详情
- 新增
  - `scripts/publish-standalone.ps1`：自包含发布到 `C:\SseApi`。
  - `scripts/tag-release.ps1`：创建并推送 Git 标签。
  - `docs/DEPLOYMENT.md`：一键部署与运行指南。
- 修改
  - `Program.cs`：
    - Kestrel 证书选择器与内容根目录调整。
    - 静态文件与默认页。
    - `POST /sse/UsersID/{UsersID}/send` 支持逗号分隔的多个 ID。
  - `SseApi.csproj`：发布包含 `certificates/**`。
  - `wwwroot/sse-send.html`：提示支持多 ID 输入。
  - `.gitignore`：忽略 `publish-standalone*/`、`*.password` 等。

## 使用提示
- 证书文件路径：`C:\SseApi\certificates\qsgl.net.pfx`（可选口令文件 `qsgl.net.password`）。
- 域名访问以匹配证书 SAN；确保 5001 端口放行。
- 发布命令：
  ```powershell
  .\scripts\publish-standalone.ps1 -Output 'C:\SseApi'
  ```
- 打标签命令：
  ```powershell
  .\scripts\tag-release.ps1 -Tag v0.3.1 -Message '说明内容'
  ```
