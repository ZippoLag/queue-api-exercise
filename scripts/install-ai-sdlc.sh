#!/usr/bin/env bash
#
# Installs the AI-assisted SDLC toolchain for this repository into the current
# user's environment. Everything is user-local (no sudo) and idempotent —
# re-running after a devcontainer rebuild installs current versions and
# rebuilds the OpenLore index. It is the executable home of the steps
# documented in docs/tooling.md; keep that page and this script in sync.
#
#   Part 1 - AI coding toolchain: pnpm, Freebuff, OpenSpec, OpenLore
#            (index, ARM64 grammar repair, optional drift hook)
#   Part 2 - AWS tooling: AWS CLI v2, uv (uvx), AWS MCP proxy prewarm
#
# Credentials are NEVER touched: after a rebuild, authenticate once with
# `aws login --remote --region eu-west-3` (12h session, renewable 90 days).
#
# Usage: bash scripts/install-ai-sdlc.sh
#   --skip-aws           skip Part 2 (AWS tooling)
#   --with-drift-hook    install the `openlore drift` pre-commit hook
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

SKIP_AWS=0
WITH_DRIFT_HOOK=0
for arg in "$@"; do
  case "$arg" in
    --skip-aws) SKIP_AWS=1 ;;
    --with-drift-hook) WITH_DRIFT_HOOK=1 ;;
    *) echo "[install-ai-sdlc] Unknown option: $arg" >&2; exit 1 ;;
  esac
done

export PATH="$HOME/.local/bin:$PATH"

echo "[install-ai-sdlc] Setting up the AI-assisted SDLC toolchain (user-local, idempotent)..."

# ---------------------------------------------------------------- Part 1: AI --
# 1. pnpm (safer alternative to npm)
if command -v pnpm >/dev/null 2>&1; then
  echo "[install-ai-sdlc] pnpm already present: $(pnpm --version 2>&1)"
else
  echo "[install-ai-sdlc] Installing pnpm..."
  wget -qO- https://get.pnpm.io/install.sh | ENV="$HOME/.bashrc" SHELL="$(which bash)" bash -
fi
source "$HOME/.bashrc" 2>/dev/null || true
pnpm runtime set node lts -g

# 2. Global tools
echo "[install-ai-sdlc] Installing global tools (freebuff, openspec, openlore)..."
pnpm install -g freebuff
pnpm install -g @fission-ai/openspec@latest
pnpm install -g openlore

# 3. OpenSpec baseline (only when starting a brand-new project; this repo has openspec/)
if [ -d openspec/ ]; then
  openspec update || true
else
  openspec init
fi

# 4. OpenLore: .openlore/config.json is committed, so just build the index
if [ ! -f .openlore/config.json ]; then
  echo "[install-ai-sdlc] Initializing OpenLore (missing .openlore/config.json)..."
  openlore init
fi
echo "[install-ai-sdlc] Building the OpenLore call-graph index (no API key)..."
openlore analyze

# 5. Health checks
echo "[install-ai-sdlc] Running health checks..."
openlore doctor || echo "[install-ai-sdlc] warning: openlore doctor reported issues (the optional LLM connection line is expected)"
openspec validate --all || echo "[install-ai-sdlc] warning: openspec validate reported issues"

# 6. ARM64 (Apple Silicon) devcontainers only — repair the C#/Bash grammars.
#    tree-sitter-c-sharp@0.21.3 ships no linux-arm64 prebuilt binary, so C#
#    files would be indexed for search but never graphed. One-time; idempotent.
if [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; then
  echo "[install-ai-sdlc] ARM64 detected — repairing OpenLore grammars..."
  bash scripts/repair-openlore-grammar.sh && openlore analyze --force
fi

# 7. Optional drift hook (recommended before each commit, no API key)
if [ "$WITH_DRIFT_HOOK" = "1" ]; then
  echo "[install-ai-sdlc] Installing the openlore drift pre-commit hook..."
  openlore drift --install-hook || echo "[install-ai-sdlc] warning: could not install the drift hook"
fi

# ----------------------------------------------------------------- Part 2: AWS --
if [ "$SKIP_AWS" = "0" ]; then
  echo "[install-ai-sdlc] Installing AWS tooling..."

  # AWS CLI v2
  if command -v aws >/dev/null 2>&1; then
    echo "[install-ai-sdlc] AWS CLI already present: $(aws --version 2>&1 | head -1)"
  else
    echo "[install-ai-sdlc] Installing AWS CLI v2..."
    curl -fsSL 'https://awscli.amazonaws.com/v2/install.sh' | bash
    echo "[install-ai-sdlc] AWS CLI installed: $(aws --version 2>&1 | head -1)"
  fi

  # uv — provides uvx, which runs the AWS MCP Server SigV4 proxy (.agents/mcp.json)
  if command -v uvx >/dev/null 2>&1; then
    echo "[install-ai-sdlc] uv already present: $(uv --version 2>&1)"
  else
    echo "[install-ai-sdlc] Installing uv (provides uvx for the AWS MCP proxy)..."
    curl -LsSf https://astral.sh/uv/install.sh | sh
    echo "[install-ai-sdlc] uv installed: $(uv --version 2>&1)"
  fi

  # Pre-warm the proxy package so the first MCP connection does not stall
  if command -v uvx >/dev/null 2>&1; then
    echo "[install-ai-sdlc] Pre-caching AWS MCP proxy (mcp-proxy-for-aws)..."
    uvx mcp-proxy-for-aws==1.6.4 --help >/dev/null 2>&1 || true
  fi
fi

# ---------------------------------------------------------------------- PATH --
# The installers drop binaries into ~/.local/bin (AWS CLI, uv) and the pnpm
# install adds its own dir; make sure new shells pick them up after a rebuild.
SHELL_RC="$HOME/.bashrc"
if [ "$(basename "${SHELL:-bash}")" = "zsh" ]; then
  SHELL_RC="$HOME/.zshrc"
fi
if ! grep -q '\.local/bin' "$SHELL_RC" 2>/dev/null; then
  echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$SHELL_RC"
  echo "[install-ai-sdlc] Added ~/.local/bin to $SHELL_RC"
fi

echo "[install-ai-sdlc] Done. After a devcontainer rebuild, re-authenticate once with:"
echo "[install-ai-sdlc]   aws login --remote --region eu-west-3   (or plain 'aws login' where a browser can reach this device)"
echo "[install-ai-sdlc] Credentials are valid 12h and renewable 90 days without re-authenticating."
