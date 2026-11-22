# api-push3.ps1 — 把本地最后一次提交改动的文件，通过 GitHub REST API 叠加到远端 head 上
# 场景：本地缺少远端最新提交（无法 fetch），但只关心「我的改动出现在远端」
param(
  [string]$Repo = 'HugoMou/salmonlauncher',
  [string]$Branch = 'main',
  [string]$Message = 'feat(2.0.17): UI 重构 + 动效开关 + 克隆备份 + 整合包仓库 + Java 检测 + 依赖检查 + 协议 3.0'
)
$ErrorActionPreference = 'Stop'
$root = 'D:\SalmonLauncher'

$remoteHead = (gh api "repos/$Repo/git/ref/heads/$Branch" --jq '.object.sha')
$remoteTree = (gh api "repos/$Repo/git/commits/$remoteHead" --jq '.tree.sha')
"remote head = $remoteHead"
"remote tree = $remoteTree"

$files = git -C $root diff --name-only HEAD~1 HEAD
$entries = @()
foreach ($path in ($files | Where-Object { $_ -ne '' })) {
  $full = Join-Path $root $path
  if (-not (Test-Path $full)) {
    $entries += @{ path = $path; mode = '100644'; type = 'blob'; sha = $null }
    "delete $path"
    continue
  }
  $b64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($full))
  $payload = @{ content = $b64; encoding = 'base64' } | ConvertTo-Json -Compress
  $tmp = Join-Path $env:TEMP ("blob-" + [guid]::NewGuid().ToString() + '.json')
  Set-Content -Path $tmp -Value $payload -Encoding utf8 -NoNewline
  $blob = gh api "repos/$Repo/git/blobs" --input $tmp | ConvertFrom-Json
  Remove-Item $tmp -Force
  $entries += @{ path = $path; mode = '100644'; type = 'blob'; sha = $blob.sha }
  "blob $path -> $($blob.sha)"
}

"files: $($entries.Count)"

$treeBody = @{ base_tree = $remoteTree; tree = $entries } | ConvertTo-Json -Depth 6 -Compress
$treeTmp = Join-Path $env:TEMP ("tree-" + [guid]::NewGuid().ToString() + '.json')
Set-Content -Path $treeTmp -Value $treeBody -Encoding utf8 -NoNewline
$newTree = (gh api "repos/$Repo/git/trees" --input $treeTmp | ConvertFrom-Json).sha
Remove-Item $treeTmp -Force
"new tree = $newTree"

$commitBody = @{ message = $Message; tree = $newTree; parents = @($remoteHead) } | ConvertTo-Json -Depth 4 -Compress
$commitTmp = Join-Path $env:TEMP ("commit-" + [guid]::NewGuid().ToString() + '.json')
Set-Content -Path $commitTmp -Value $commitBody -Encoding utf8 -NoNewline
$newCommit = (gh api "repos/$Repo/git/commits" --input $commitTmp | ConvertFrom-Json).sha
Remove-Item $commitTmp -Force
"new commit = $newCommit"

$refBody = @{ sha = $newCommit; force = $false } | ConvertTo-Json -Compress
$refTmp = Join-Path $env:TEMP ("ref-" + [guid]::NewGuid().ToString() + '.json')
Set-Content -Path $refTmp -Value $refBody -Encoding utf8 -NoNewline
gh api -X PATCH "repos/$Repo/git/refs/heads/$Branch" --input $refTmp | Out-Null
Remove-Item $refTmp -Force
"ref updated to $newCommit"