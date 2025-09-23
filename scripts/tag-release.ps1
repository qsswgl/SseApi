param(
  [string]$Tag,
  [string]$Message = $null
)

if (-not $Tag) { Write-Error "Usage: .\scripts\tag-release.ps1 -Tag v0.3.0 [-Message 'notes']"; exit 1 }

# Ensure clean working tree
$status = git status --porcelain
if ($status) {
  Write-Error "Working tree not clean. Commit or stash changes first."; exit 1
}

if (-not $Message) { $Message = $Tag }

Write-Host "Creating annotated tag $Tag ..."

git tag -a $Tag -m $Message
if ($LASTEXITCODE -ne 0) { Write-Error "git tag failed"; exit 1 }

Write-Host "Pushing tag $Tag to origin..."

git push origin $Tag
if ($LASTEXITCODE -ne 0) { Write-Error "git push failed"; exit 1 }

Write-Host "Done."
