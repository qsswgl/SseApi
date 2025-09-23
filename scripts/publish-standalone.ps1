param(
  [string]$Output = 'C:\SseApi'
)

Write-Host "Publishing SseApi to $Output (self-contained win-x64)..."

# Ensure output exists
New-Item -ItemType Directory -Force -Path $Output | Out-Null

# Publish
& dotnet publish --configuration Release --self-contained true --runtime win-x64 --output $Output
if ($LASTEXITCODE -ne 0) {
  Write-Error "dotnet publish failed"
  exit 1
}

# Show result
Get-ChildItem $Output | Select-Object Name, Length | Format-Table

Write-Host "Done. Start with:"
Write-Host "cd $Output"
Write-Host ".\\SseApi.exe"
