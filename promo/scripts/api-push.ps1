# api-push.ps1 — 当 github.com:443 被阻断时，用 GitHub REST API 提交当前暂存改动
# 依赖：gh CLI 已登录（gh auth status OK）
param(
  [string]$Repo = 'HugoMou/salmonlauncher',
  [string]$Branch = 'main',
  [Parameter(Mandatory = $true)][string]$Message
)
$ErrorActionPreference = 'Stop'

function GhJson([string]$bodyFile) {
  $out = gh api $args[0] --input $bodyFile 2>&1
  return $out | ConvertFrom-Json
}

# 1) 当前分支头
$headSha = (gh api "repos/$Repo/git/ref/heads/$Branch" --jq '.object.sha')
"head=$headSha"
$commitInfo = gh api "repos/$Repo/git/commits/$headSha" | ConvertFrom-Json
$baseTree = $commitInfo.tree.sha
"base_tree=$baseTree"

# 2) 收集暂存改动
$status = git diff --cached --name-status | Where-Object { $_ -ne '' }
$added = @(); $removed = @()
foreach ($line in $status) {
  $parts = $line -split "`t"
  $flag = $parts[0]; $path = $parts[1]
  if ($flag -eq 'D') { $removed += $path }
  else { $added += $path }
}
"added=$($added.Count) removed=$($removed.Count)"

# 3) 上传新增/修改文件的 blob
$entries = @()
foreach ($path in $added) {
  $b64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes((Join-Path 'D:\SalmonLauncher' $path)))
  $payload = @{ content = $b64; encoding = 'base64' } | ConvertTo-Json -Compress
  $tmp = Join-Path $env:TEMP ("blob-" + [guid]::NewGuid().ToString() + '.json')
  $payload | Set-Content -Path $tmp -Encoding utf8
  $blob = gh api "repos/$Repo/git/blobs" --input $tmp | ConvertFrom-Json
  Remove-Item $tmp -Force
  $entries += @{ path = $path; mode = '100644'; type = 'blob'; sha = $blob.sha }
  "blob $path -> $($blob.sha)"
}

# 4) 删除项：sha = $null
foreach ($path in $removed) {
  $entries += @{ path = $path; mode = '100644'; type = 'blob'; sha = $null }
}

# 5) 建 tree
$treePayload = @{ base_tree = $baseTree; tree = $entries } | ConvertTo-Json -Depth 8 -Compress
$tmpTree = Join-Path $env:TEMP ("tree-" + [guid]::NewGuid().ToString() + '.json')
$treePayload | Set-Content -Path $tmpTree -Encoding utf8
$newTree = gh api "repos/$Repo/git/trees" --input $tmpTree | ConvertFrom-Json
Remove-Item $tmpTree -Force
"new_tree=$($newTree.sha)"

# 6) 建 commit
$commitPayload = @{ message = $Message; tree = $newTree.sha; parents = @($headSha) } | ConvertTo-Json -Depth 5 -Compress
$tmpCommit = Join-Path $env:TEMP ("commit-" + [guid]::NewGuid().ToString() + '.json')
$commitPayload | Set-Content -Path $tmpCommit -Encoding utf8
$newCommit = gh api "repos/$Repo/git/commits" --input $tmpCommit | ConvertFrom-Json
Remove-Item $tmpCommit -Force
"new_commit=$($newCommit.sha)"

# 7) 更新分支
$refPayload = @{ sha = $newCommit.sha } | ConvertTo-Json -Compress
$tmpRef = Join-Path $env:TEMP ("ref-" + [guid]::NewGuid().ToString() + '.json')
$refPayload | Set-Content -Path $tmpRef -Encoding utf8
gh api "repos/$Repo/git/refs/heads/$Branch" -X PATCH --input $tmpRef | Out-Null
Remove-Item $tmpRef -Force
"branch updated to $($newCommit.sha)"

# 8) 本地同步：把远端新提交拉到本地引用，避免下次 push 冲突
git update-ref "refs/remotes/origin/$Branch" $newCommit.sha
git reset --soft $newCommit.sha
git status --short
"done"