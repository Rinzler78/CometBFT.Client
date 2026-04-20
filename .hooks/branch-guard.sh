#!/usr/bin/env bash
# Branch guard — blocks direct commits to master and develop.
# Used by both the pre-commit framework (via .pre-commit-config.yaml)
# and the standalone pre-commit hook (.hooks/pre-commit).
set -euo pipefail

BRANCH=$(git symbolic-ref --short HEAD 2>/dev/null || true)

if [ "$BRANCH" = "master" ] || [ "$BRANCH" = "develop" ]; then
    echo ""
    echo "  ERROR: Direct commits to \"$BRANCH\" are not allowed."
    echo ""
    echo "  Workflow (git flow + worktree):"
    echo "    git worktree add .worktrees/feature/<name> -b feature/<name>"
    echo "    cd .worktrees/feature/<name>"
    echo "    # work, commit, push, open PR"
    echo ""
    echo "  See AGENTS.md §Development Workflow for the full protocol."
    echo ""
    exit 1
fi

exit 0
