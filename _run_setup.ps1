Set-Location "C:\Users\hvnes\YomawariByoin"
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=== YomawariByoin GitHub Setup ===" -ForegroundColor Cyan

# --- gh CLI の確認 & 自動ダウンロード ---
Write-Host "`n[gh] gh CLI を確認..." -ForegroundColor Yellow
$gh = Get-Command gh -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source

if (-not $gh) {
    Write-Host "gh CLI が見つかりません。ポータブル版をダウンロードします..." -ForegroundColor Yellow
    $ghDir = "$env:LOCALAPPDATA\gh-portable"
    New-Item -ItemType Directory -Path $ghDir -Force | Out-Null

    try {
        Write-Host "GitHub API で最新版を取得中..."
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/cli/cli/releases/latest" -UseBasicParsing
        $asset = $release.assets | Where-Object { $_.name -like "*windows_amd64.zip" } | Select-Object -First 1
        if (-not $asset) { throw "ZIP アセットが見つかりません" }
        $zipUrl = $asset.browser_download_url
        $zipPath = "$ghDir\gh.zip"
        Write-Host "ダウンロード中: $zipUrl"
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
        Write-Host "展開中..."
        Expand-Archive -Path $zipPath -DestinationPath $ghDir -Force
        $ghExe = Get-ChildItem -Path $ghDir -Filter "gh.exe" -Recurse | Select-Object -First 1
        if (-not $ghExe) { throw "gh.exe が展開後に見つかりません" }
        $env:PATH = "$($ghExe.Directory.FullName);$env:PATH"
        $gh = $ghExe.FullName
        Write-Host "gh CLI インストール完了: $gh" -ForegroundColor Green
    } catch {
        Write-Host "ダウンロード失敗: $_" -ForegroundColor Red
        Write-Host "手動インストール: winget install GitHub.cli" -ForegroundColor Red
        Read-Host "Enterで終了"
        exit 1
    }
}

Write-Host "gh: $gh" -ForegroundColor Green

# --- GitHub 認証 ---
Write-Host "`n[auth] GitHub 認証確認..." -ForegroundColor Yellow
$authCheck = gh auth status 2>&1
Write-Host $authCheck
if ($LASTEXITCODE -ne 0) {
    Write-Host "ブラウザで認証します..." -ForegroundColor Yellow
    gh auth login --web --git-protocol https
}

# --- GitHub リポジトリ作成 & push ---
Write-Host "`n[repo] GitHub リポジトリ作成..." -ForegroundColor Yellow
$repoResult = gh repo create yomawari-byoin --public --source=. --remote=origin --push 2>&1
Write-Host $repoResult
if ($LASTEXITCODE -ne 0) {
    Write-Host "リポジトリが既存の可能性。リモートを設定してpush..." -ForegroundColor Yellow
    $ghUser = gh api user -q .login
    git remote remove origin 2>$null
    git remote add origin "https://github.com/$ghUser/yomawari-byoin.git"
    git push -u origin main
}

# --- Phase1 ラベル作成 ---
Write-Host "`n[label] Phase1 ラベル作成..." -ForegroundColor Yellow
gh label create "Phase1" --color "0075ca" --description "Phase 1 Prototype" 2>&1

# --- Issues 作成 ---
Write-Host "`n[issues] GitHub Issues 作成..." -ForegroundColor Yellow

$i1 = gh issue create `
    --title "基本移動の実装" `
    --body "一人称視点カメラ・WASD移動・走り（Shift）・しゃがみ（C）" `
    --label "Phase1" --json number -q .number
Write-Host "Issue #$i1 作成: 基本移動の実装"

$i2 = gh issue create `
    --title "敵の巡回と捕捉の実装" `
    --body "NavMeshで巡回・視野判定・捕捉処理・病室転送" `
    --label "Phase1" --json number -q .number
Write-Host "Issue #$i2 作成: 敵の巡回と捕捉の実装"

$i3 = gh issue create `
    --title "フラグ管理の実装" `
    --body "FlagDataクラス・各フラグのトリガー・セーブ" `
    --label "Phase1" --json number -q .number
Write-Host "Issue #$i3 作成: フラグ管理の実装"

$i4 = gh issue create `
    --title "簡易エンド判定の実装" `
    --body "6エンドの条件チェック・エンド画面の仮表示" `
    --label "Phase1" --json number -q .number
Write-Host "Issue #$i4 作成: 簡易エンド判定の実装"

$i5 = gh issue create `
    --title "幻覚レベルの基本実装" `
    --body "レベル管理・時間経過での上昇・画面エフェクト（プレイヤーごとに独立管理）" `
    --label "Phase1" --json number -q .number
Write-Host "Issue #$i5 作成: 幻覚レベルの基本実装"

Write-Host "作成したIssues: #$i1 #$i2 #$i3 #$i4 #$i5" -ForegroundColor Green

# --- Issue #1 (基本移動) をクローズ ---
Write-Host "`n[close] Issue #$i1 をクローズ..." -ForegroundColor Yellow
gh issue close $i1 --comment "PlayerController.cs と CameraController.cs を実装。WASD移動・走り・しゃがみ・一人称カメラ完了。"

# --- 結果表示 ---
Write-Host "`n=== セットアップ完了！ ===" -ForegroundColor Green
$repoUrl = gh repo view --json url -q .url
Write-Host "リポジトリURL: $repoUrl" -ForegroundColor Cyan
Write-Host "`n--- Issue 一覧 ---"
gh issue list

Read-Host "`nEnterで終了"
