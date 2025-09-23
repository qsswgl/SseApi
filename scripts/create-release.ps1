param(
  [string]$Tag = 'v0.3.0',
  [string]$NotesFile = 'docs/RELEASE_NOTES_v0.3.0.md'
)

# Requires GitHub CLI: https://cli.github.com/
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  Write-Error "GitHub CLI (gh) not installed. Please install: https://cli.github.com/"; exit 1
}

if (-not (Test-Path $NotesFile)) {
  Write-Error "Release notes not found: $NotesFile"; exit 1
}

Write-Host "Creating GitHub Release: $Tag ..."

gh release create $Tag -F $NotesFile -t $Tag --verify-tag
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to create Release"; exit 1 }

Write-Host "Release $Tag created."
