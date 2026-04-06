#!/usr/bin/env bash
set -euo pipefail

BLUE='\033[0;34m'
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'

select_mode() {
  local choice
  while true; do
    echo "" >&2
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" >&2
    echo "  Chọn mode hoạt động cho AI Agent:" >&2
    echo "" >&2
    echo "  1) DEV AGENT" >&2
    echo "     Claude hỗ trợ code theo từng task." >&2
    echo "     Luôn hỏi xác nhận trước khi làm." >&2
    echo "" >&2
    echo "  2) CONSULTANT AGENT" >&2
    echo "     Claude tự triển khai hoàn toàn." >&2
    echo "     Tự review và bàn giao kết quả." >&2
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" >&2
    printf "Enter [1/2]: " >&2
    read -r choice
    case "$choice" in
      1) echo "DEV"; return ;;
      2) echo "CONSULTANT"; return ;;
      *) printf "${RED}Vui lòng nhập 1 hoặc 2.${NC}\n" >&2 ;;
    esac
  done
}

build_system_prompt() {
  local mode="$1"
  local prompt_file="$2"
  sed "s/{{MODE_LINE}}/MODE: ${mode}/" "$prompt_file"
}

printf "${BLUE}🫐 Blueberry Sensei — Initializing...${NC}\n"

# Check claude CLI is installed
if ! command -v claude &>/dev/null; then
  printf "${RED}Error: 'claude' CLI is not installed.${NC}\n"
  echo ""
  echo "Install it with:"
  echo "  npm install -g @anthropic-ai/claude-code"
  echo ""
  echo "Then run this script again."
  exit 1
fi

# Load .env
if [ -f .env ]; then
  set -a
  source .env
  set +a
fi

# Validate token
if [ -z "${BLUEBERRY_SENSEI_CLAUDE_WIZARD_TOKEN:-}" ]; then
  printf "${RED}Error: BLUEBERRY_SENSEI_CLAUDE_WIZARD_TOKEN not found in .env${NC}\n"
  echo "Create a .env file with:"
  echo "  BLUEBERRY_SENSEI_CLAUDE_WIZARD_TOKEN=<your_github_token>"
  exit 1
fi

# Override these in .env if using a different repo:
#   BLUEBERRY_SENSEI_GITHUB_USER=your-org
#   BLUEBERRY_SENSEI_REPO=your-repo-name
GITHUB_USER="${BLUEBERRY_SENSEI_GITHUB_USER:-dat-phan-blueberry}"
REPO_NAME="${BLUEBERRY_SENSEI_REPO:-blueberry-sensei-claude-wizard}"

# Create temp dir for clone log, then clone into a fresh subdir
TMPBASE=$(mktemp -d)
CLONE_LOG="$TMPBASE/clone.log"
FRAMEWORK_DIR="$TMPBASE/framework"

# Ensure temp dir is cleaned up on exit (security: token is in .git/config)
trap 'rm -rf "$TMPBASE"' EXIT

echo "Pulling framework..."
git clone --quiet \
  "https://${BLUEBERRY_SENSEI_CLAUDE_WIZARD_TOKEN}@github.com/${GITHUB_USER}/${REPO_NAME}.git" \
  "$FRAMEWORK_DIR" 2>"$CLONE_LOG" || {
  printf "${RED}Error: Could not pull framework. Check your token and repo access.${NC}\n"
  cat "$CLONE_LOG" 2>/dev/null
  exit 1
}

# Copy static files into the project
echo "Setting up project structure..."
mkdir -p .claude/skills .claude/memory/archive .claude/hooks docs/plans

cp "$FRAMEWORK_DIR/wizard/templates/hooks/pre-task.sh" .claude/hooks/pre-task.sh
cp "$FRAMEWORK_DIR/wizard/templates/hooks/post-task.sh" .claude/hooks/post-task.sh
cp "$FRAMEWORK_DIR/wizard/templates/settings.json"      .claude/settings.json
chmod +x .claude/hooks/*.sh

# Select agent mode
AGENT_MODE=$(select_mode)
printf "${BLUE}Mode: ${AGENT_MODE} AGENT${NC}\n"

# Launch Claude with the sensei init system prompt
printf "${BLUE}Launching Claude with Sensei system prompt...${NC}\n"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Mode: ${AGENT_MODE} AGENT"
echo "  Type 'Start' (or press Enter) to begin."
echo "  Claude sẽ đọc codebase, hỏi bạn vài câu,"
echo "  rồi generate config cho dự án."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
SYSTEM_PROMPT=$(build_system_prompt "$AGENT_MODE" "$FRAMEWORK_DIR/wizard/sensei-init-prompt.md")
claude --system-prompt "$SYSTEM_PROMPT"

echo ""
printf "${GREEN}✓ Blueberry Sensei initialized successfully!${NC}\n"
echo ""
echo "Next steps:"
echo "  1. Review CLAUDE.md"
echo "  2. Add .env to .gitignore (if not already)"
echo "  3. Commit .claude/ and CLAUDE.md to git"
