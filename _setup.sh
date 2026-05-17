#!/bin/bash
set -e
cd "$(dirname "$0")"
echo "=== YomawariByoin Git Setup ==="
echo "Working dir: $(pwd)"

# [1] git init
echo "[1] git init..."
git init
git branch -M main

# git user config
git config user.email "hvnesdi@icloud.com"
git config user.name "Hiroki Kishimoto"

git add .gitignore CLAUDE.md
git add Assets/Scripts/PlayerController.cs Assets/Scripts/CameraController.cs
git add Packages/ ProjectSettings/
git commit -m "Initial commit"

# [2] git status
echo "[2] Committed files:"
git log --oneline -5

# [3] gh check
echo "[3] Checking gh CLI..."
if ! command -v gh &> /dev/null; then
    echo "gh CLI not found. Trying winget path..."
    export PATH="$PATH:/c/Users/hvnes/AppData/Local/Microsoft/WinGet/Links"
fi

if command -v gh &> /dev/null; then
    echo "gh CLI found: $(gh --version | head -1)"
    echo "[4] gh auth status:"
    gh auth status

    echo "[5] Creating GitHub repo..."
    gh repo create yomawari-byoin --public --source=. --remote=origin --push || echo "Repo may already exist, trying push..."
    git push -u origin main 2>/dev/null || true

    echo "[6] CLAUDE.md & scripts commit..."
    git add -A
    git diff --cached --quiet || git commit -m "Add team development rules and player scripts"
    git push 2>/dev/null || true

    echo "[7] Creating Phase1 label..."
    gh label create "Phase1" --color "0075ca" --description "Phase 1 features" 2>/dev/null || echo "Label may exist"

    echo "[8] Creating Issues..."
    gh issue create --title "Basic movement implementation" --body "FPS camera, WASD movement, run, crouch" --label "Phase1"
    gh issue create --title "Enemy patrol and capture" --body "NavMesh patrol, FOV detection, capture, room transfer" --label "Phase1"
    gh issue create --title "Flag management" --body "FlagData class, triggers, save" --label "Phase1"
    gh issue create --title "Basic ending system" --body "6 ending conditions check, temp ending screen" --label "Phase1"
    gh issue create --title "Hallucination level system" --body "Level management, time-based increase, visual effects (per player)" --label "Phase1"

    echo "[9] Close Issue #1..."
    gh issue close 1 --comment "PlayerController.cs and CameraController.cs implemented. WASD, run, crouch, FPS camera done."

    echo "[10] Repo URL:"
    gh repo view --json url -q .url
    echo "Issues:"
    gh issue list
else
    echo "gh CLI not available. Git initialized and committed locally."
    echo "Please install gh CLI: winget install GitHub.cli"
    echo "Then run: gh repo create yomawari-byoin --public --source=. --push"
fi

echo ""
echo "=== Setup complete! ==="
read -p "Press Enter to close..."
