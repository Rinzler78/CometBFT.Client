#!/usr/bin/env bash
# Install all git hooks from .hooks/ and set up the pre-commit framework.
#
# Run once after cloning, or after any hook update:
#   ./scripts/install-hooks.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HOOKS_SRC="$REPO_ROOT/.hooks"
HOOKS_DST="$REPO_ROOT/.git/hooks"

echo "==> Installing git hooks from .hooks/ → .git/hooks/"

for hook in "$HOOKS_SRC"/*; do
    name="$(basename "$hook")"
    cp "$hook" "$HOOKS_DST/$name"
    chmod +x "$HOOKS_DST/$name"
    echo "    ✓ $name"
done

# ── pre-commit framework ──────────────────────────────────────────────────────
echo "==> Installing pre-commit framework hooks"

if ! command -v pre-commit > /dev/null 2>&1; then
    echo "    pre-commit not found — installing via pip"
    pip install pre-commit
fi

# pre-commit refuses to install when core.hooksPath is set explicitly.
# Unset it temporarily (the default .git/hooks is equivalent).
HOOKS_PATH_SAVED=$(git config --local core.hooksPath 2>/dev/null || true)
[ -n "$HOOKS_PATH_SAVED" ] && git config --local --unset core.hooksPath

# Install framework hooks (commit-msg, pre-commit, etc.) — this regenerates
# .git/hooks/pre-commit with the framework bootstrap. Our .hooks/pre-commit
# wraps the framework, so reinstall from source to restore the branch guard.
pre-commit install --install-hooks

# Restore core.hooksPath if it was set.
[ -n "$HOOKS_PATH_SAVED" ] && git config --local core.hooksPath "$HOOKS_PATH_SAVED"

# Restore our pre-commit wrapper (branch guard + framework delegation).
cp "$HOOKS_SRC/pre-commit" "$HOOKS_DST/pre-commit"
chmod +x "$HOOKS_DST/pre-commit"
echo "    ✓ pre-commit (branch guard + framework)"

# ── detect-secrets baseline ───────────────────────────────────────────────────
if [ ! -f "$REPO_ROOT/.secrets.baseline" ]; then
    echo "==> Generating detect-secrets baseline"
    detect-secrets scan > "$REPO_ROOT/.secrets.baseline"
fi

echo ""
echo "All hooks installed. Active hooks:"
ls -1 "$HOOKS_DST" | grep -v '\.sample$' | sed 's/^/    /'
