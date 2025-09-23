param(
  [string]$Tag = 'v0.3.0',
  [string]$NotesFile = 'docs/RELEASE_NOTES_v0.3.0.md'
)

# Requires GitHub CLI: https://cli.github.com/
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  Write-Error "GitHub CLI (gh) 未安装。请安装后重试：https://cli.github.com/"; exit 1
}

if (-not (Test-Path $NotesFile)) {
  Write-Error "未找到发布说明：$NotesFile"; exit 1
}

Write-Host "创建 GitHub Release: $Tag ..."

gh release create $Tag -F $NotesFile -t $Tag --verify-tag
if ($LASTEXITCODE -ne 0) { Write-Error "创建 Release 失败"; exit 1 }

Write-Host "Release $Tag 创建完成。"
