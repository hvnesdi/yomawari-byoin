#!/bin/bash
# GitHub API setup using gh CLI (if available) or git credential
cd "$(dirname "$0")"

echo "=== GitHub API Setup ==="

# Check if gh is available
if command -v gh &> /dev/null; then
    GH_AVAILABLE=true
    echo "gh CLI found."
else
    # Try common locations
    for p in \
        "/c/Users/hvnes/AppData/Local/Microsoft/WinGet/Links/gh" \
        "/c/Program Files/GitHub CLI/gh" \
        "/c/Users/hvnes/scoop/shims/gh"; do
        if [ -f "$p" ]; then
            alias gh="$p"
            GH_AVAILABLE=true
            echo "Found gh at: $p"
            break
        fi
    done
fi

if [ "${GH_AVAILABLE}" != "true" ]; then
    echo ""
    echo "=== gh CLI not found ==="
    echo "Installing gh CLI via winget..."
    winget install --id GitHub.cli --silent --accept-package-agreements --accept-source-agreements
    echo "Reloading PATH..."
    export PATH="$PATH:/c/Users/hvnes/AppData/Local/Microsoft/WinGet/Links"

    if command -v gh &> /dev/null; then
        echo "gh CLI installed successfully!"
        GH_AVAILABLE=true
    else
        echo "Installation failed or requires restart."
        echo "Please run this script again after installing gh CLI."
        read -p "Press Enter to exit..."
        exit 1
    fi
fi

# Auth check
echo ""
echo "[auth] Checking GitHub auth..."
if ! gh auth status &>/dev/null; then
    echo "Not logged in. Starting gh auth login..."
    gh auth login
fi

echo ""
echo "[1] Creating GitHub repo..."
gh repo create yomawari-byoin --public --source=. --remote=origin --push 2>&1 || {
    echo "Repo might exist. Trying to add remote and push..."
    GHUSER=$(gh api user -q .login)
    git remote add origin "https://github.com/$GHUSER/yomawari-byoin.git" 2>/dev/null || true
    git push -u origin main
}

echo ""
echo "[2] Creating Phase1 label..."
gh label create "Phase1" --color "0075ca" --description "Phase 1 Prototype" 2>/dev/null || echo "Label may already exist"

echo ""
echo "[3] Creating 5 Issues..."
ISSUE1=$(gh issue create --title "Basic movement implementation" --body "FPS camera, WASD movement, run (Shift), crouch (C)" --label "Phase1" --json number -q .number)
ISSUE2=$(gh issue create --title "Enemy patrol and capture" --body "NavMesh patrol, FOV detection, capture processing, room transfer" --label "Phase1" --json number -q .number)
ISSUE3=$(gh issue create --title "Flag management" --body "FlagData class, each flag trigger, save" --label "Phase1" --json number -q .number)
ISSUE4=$(gh issue create --title "Basic ending system" --body "6 ending condition checks, temporary ending screen" --label "Phase1" --json number -q .number)
ISSUE5=$(gh issue create --title "Hallucination level system" --body "Level management, time-based increase, visual effects (per player independent)" --label "Phase1" --json number -q .number)

echo "Created issues: #$ISSUE1 #$ISSUE2 #$ISSUE3 #$ISSUE4 #$ISSUE5"

echo ""
echo "[4] Closing Issue #$ISSUE1 (Basic movement - already implemented)..."
gh issue close $ISSUE1 --comment "PlayerController.cs and CameraController.cs implemented. WASD, run, crouch, FPS camera done."

echo ""
echo "[5] Result:"
REPO_URL=$(gh repo view --json url -q .url)
echo "Repository URL: $REPO_URL"
echo ""
echo "Issues:"
gh issue list

echo ""
echo "=== ALL DONE ==="
read -p "Press Enter to close..."
