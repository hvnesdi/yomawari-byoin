@echo off
cd /d "C:\Users\hvnes\YomawariByoin"
echo === Starting git setup ===

echo [1] git init...
git init
git branch -M main

echo [2] git add and commit...
git add .gitignore
git add Assets\Scripts\PlayerController.cs
git add Assets\Scripts\CameraController.cs
git add CLAUDE.md
git add Packages\
git add ProjectSettings\
git commit -m "Initial commit"

echo [3] Check gh CLI...
gh --version
if errorlevel 1 (
    echo gh CLI not found!
    pause
    exit /b 1
)

echo [4] gh auth status...
gh auth status

echo [5] Create GitHub repo...
gh repo create yomawari-byoin --public --source=. --remote=origin --push

echo [6] Create Phase1 label...
gh label create "Phase1" --color "0075ca" --description "Phase 1: Prototype features"

echo [7] Create issues...
gh issue create --title "基本移動の実装" --body "一人称視点カメラ・WASD移動・走り・しゃがみ" --label "Phase1"
gh issue create --title "敵の巡回と捕捉の実装" --body "NavMeshで巡回・視野判定・捕捉処理・病室転送" --label "Phase1"
gh issue create --title "フラグ管理の実装" --body "FlagDataクラス・各フラグのトリガー・保存" --label "Phase1"
gh issue create --title "簡易エンド判定の実装" --body "6エンドの条件チェック・エンド画面の仮表示" --label "Phase1"
gh issue create --title "幻覚レベルの基本実装" --body "レベル管理・時間経過での上昇・画面エフェクト（プレイヤーごとに独立管理）" --label "Phase1"

echo [8] Close Issue #1...
gh issue close 1 --comment "PlayerController.cs と CameraController.cs を実装。WASD移動・走り・しゃがみ・一人称カメラ完了。"

echo [9] Show repo info...
gh repo view --json url -q .url
gh issue list

echo === Done! ===
pause
