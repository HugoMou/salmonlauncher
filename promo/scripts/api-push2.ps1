# api-push2.ps1 — 用 GitHub REST API 把「本地 HEAD 相对远端 head」的差异推送上去
# 适用：github.com:443 被阻断、但 api.github.com 可用（gh 已登录）
param(
  [string]$Repo = 'HugoMou/salmonlauncher',
  [string]$Branch = 'main'
)
$ErrorActionPreference = 'Stop'
$root = 'D:\SalmonLauncher'

$remoteHead = (gh api "repos/$Repo/git/ref/heads/$Branch" --jq '.object.sha')
$localHead = (git -C $root rev-parse HEAD)
"remote=$remoteHead"
"local =$localHead"
if ($remoteHead -eq $localHead) { 'already in sync'; exit 0 }

$baseTree = (gh api "repos/$Repo/git/commits/$remoteHead" --jq '.tree.sha')

# 差异文件
$diff = git -C $root diff --name-status $remoteHead $localHead
$entries = @()
foreach ($line in ($diff | Where-Object { $_ -ne '' })) {
  $parts = $line -split "`t"
  $flag = $parts[0]; $path = $parts[1]
  if ($flag -eq 'D') {
    $entries += @{ path = $path; mode = '100644'; type = 'blob'; sha = $null }
    "del $path"
    continue
  }
  $full = Join-Path $root $path
  if (-not (Test-Path $full)) { "skip missing $path"; continue }
  $b64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($full))
  $payload = @{ content = $b64; encoding = 'base64' } | ConvertTo-Json -Compress
  $tmp = Join-Path $env:TEMP ("blob-" + [guid]::NewGuid().ToString() + '.json')
  $payload | Set-Content -Path $tmp -Encoding utf8
  $blob = gh api "repos/$Repo/git/blobs" --input $tmp | ConvertFrom-Json
  Remove-Item $tmp -Force
  $entries += @{ path = $path; mode = '100644'; type = 'blob'; sha = $blob.sha }
  "blob $path -> $($blob.sha)"
}

if ($entries.Count -eq 0) { 'no file differences'; exit 0 }

# tree
$treePayload = @{ base_tree = $baseTree; tree = $entries } | ConvertTo-Json -Depth 8 -Compress
$tmpTree = Join-Path $env:TEMP ("tree-" + [guid]::NewGuid().ToString() + '.json')
$treePayload | Set-Content -Path $tmpTree -Encoding utf8
$newTree = gh api "repos/$Repo/git/trees" --input $tmpTree | ConvertFrom-Json
Remove-Item $tmpTree -Force
"tree=$($newTree.sha)"

# commit（沿用本地提交信息）
$msg = (git -C $root log -1 --pretty=%B) -join "`n"
$commitPayload = @{ message = $msg; tree = $newTree.sha; parents = @($remoteHead) } | ConvertTo-Json -Depth 5 -Compress
$tmpCommit = Join-Path $env:TEMP ("commit-" + [guid]::NewGuid().ToString() + '.json')
$commitPayload | Set-Content -Path $tmpCommit -Encoding utf8
$newCommit = gh api "repos/$Repo/git/commits" --input $tmpCommit | ConvertFrom-Json
Remove-Item $tmpCommit -Force
"commit=$($newCommit.sha)"

# 更新远端分支
$refPayload = @{ sha = $newCommit.sha; force = $false } | ConvertTo-Json -Compress
$tmpRef = Join-Path $env:TEMP ("ref-" + [guid]::NewGuid().ToString() + '.json')
$refPayload | Set-Content -Path $tmpRef -Encoding utf8
$res = gh api -X PATCH "repos/$Repo/git/refs/heads/$Branch" --input $tmpRef 2>&1
Remove-Item $tmpRef -Force
"ref patched: $($res.object.sha)"

# 本地对齐远端
git -C $root update-ref "refs/remotes/origin/$Branch" $newCommit.sha
git -C $root reset --soft $newCommit.sha
"local now at $((git -C $root rev-parse HEAD))"
git -C $root status --short | Select-Object -First 10
'synced'