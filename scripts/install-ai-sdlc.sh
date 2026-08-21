#!/usr/bin/env bash
# Installs the AI-assisted SDLC toolchain into the current user's environment.
#
# Everything is user-local (no sudo) and idempotent — re-running installs
# current versions and rebuilds the OpenLore index. This script is the
# executable home of the steps documented in docs/tooling.md; keep that page
# and this script in sync.
#
#   1. Node via nvm (version from .nvmrc) + pnpm
#   2. Freebuff, OpenSpec, OpenLore as pnpm globals (explicit build approvals)
#   3. Project setup: openspec update/init, openlore init/analyze, health checks
#   4. --with-aws: AWS CLI v2, uv, gh, terraform (all optional)
#
# Credentials are never touched: after a rebuild, authenticate once with
# `aws login --remote --region eu-west-3` (12h session, renewable 90 days).
#
# Usage: bash scripts/install-ai-sdlc.sh [--with-aws]

set -Eeuo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Tools installed by this script (AWS CLI, uv, gh, terraform) land in
# ~/.local/bin; make them visible to the current session too, not just new
# shells (the rc-file block at the end persists it).
export PATH="$HOME/.local/bin:$PATH"

WITH_AWS=0
for arg in "$@"; do
  case "$arg" in
    --with-aws) WITH_AWS=1 ;;
    *) echo "[install-ai-sdlc] Unknown option: $arg" >&2; exit 1 ;;
  esac
done

# rc_append <block>: append <block> to both ~/.bashrc and ~/.zshrc when its
# first line is not already present. The devcontainer defaults to bash, but the
# user's terminal may run zsh, so the exports must reach both files for new
# shells to find the tools after a rebuild.
rc_append() {
  local block="$1" first
  first="$(printf '%s\n' "$block" | sed -n '1p')"
  for rc in "$HOME/.bashrc" "$HOME/.zshrc"; do
    [ -f "$rc" ] || touch "$rc"
    grep -qF -- "$first" "$rc" || printf '\n%s\n' "$block" >> "$rc"
  done
}

echo "[install-ai-sdlc] Setting up the AI-assisted SDLC toolchain (user-local, idempotent)..."

# ------------------------------------------------------------ 1. Node via nvm --
# nvm manages node, and node bundles npm/npx (npx is what the OpenLore drift
# hook invokes). npm is never used as a package manager here — pnpm is.
export NVM_DIR="$HOME/.nvm"
if [ ! -s "$NVM_DIR/nvm.sh" ]; then
  echo "[install-ai-sdlc] Installing nvm..."
  curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.7/install.sh | bash
fi
# nvm breaks under strict mode: sourcing nvm.sh returns 3 when no version is
# installed yet, and its commands read unbound variables — so disable -e/-u
# around sourcing and the nvm commands, then re-enable and check node.
set +eu
. "$NVM_DIR/nvm.sh"
[ -f .nvmrc ] || printf 'node\n' > .nvmrc
nvm install
nvm use --silent
nvm alias default "$(nvm version)" >/dev/null
set -eu
command -v node >/dev/null 2>&1 || { echo "[install-ai-sdlc] ERROR: node did not become available via nvm" >&2; exit 1; }
echo "[install-ai-sdlc] node: $(node --version) via nvm"

# ------------------------------------------------------------------- 2. pnpm --
if command -v pnpm >/dev/null 2>&1; then
  echo "[install-ai-sdlc] pnpm already present: $(pnpm --version 2>&1)"
else
  echo "[install-ai-sdlc] Installing pnpm..."
  curl -fsSL https://get.pnpm.io/install.sh | ENV="$HOME/.bashrc" SHELL="$(which bash)" bash -
fi
# The pnpm installer appends its PNPM_HOME/PATH lines to one rc file, but
# sourcing rc files from this non-interactive script is a no-op; export the
# same values here and persist them for new shells at the end of the script.
case "$(uname -s)" in
  Darwin) export PNPM_HOME="${PNPM_HOME:-$HOME/Library/pnpm}" ;;
  *)      export PNPM_HOME="${PNPM_HOME:-$HOME/.local/share/pnpm}" ;;
esac
export PATH="$PNPM_HOME/bin:$PATH"
echo "[install-ai-sdlc] pnpm: $(pnpm --version 2>&1)"

# ------------------------------------------------- 3. Global tools via pnpm ---
# pnpm >= 10 blocks dependency lifecycle scripts until they are approved;
# --allow-build approves exactly the packages that must build. Every package in
# the dependency tree with a build script has to be listed or pnpm prompts
# interactively, so besides the tools themselves this includes sharp and
# tree-sitter-cli, which openlore pulls in. Unlisted optional grammars for
# unrelated languages are deliberately not approved. stdin is redirected so a
# missed approval fails loudly instead of hanging on a checkbox prompt.
echo "[install-ai-sdlc] Installing global tools (freebuff, openspec, openlore)..."
pnpm add -g freebuff
pnpm add -g --allow-build=@fission-ai/openspec @fission-ai/openspec@latest
pnpm add -g \
  --allow-build=openlore \
  --allow-build=tree-sitter \
  --allow-build=tree-sitter-c-sharp \
  --allow-build=tree-sitter-bash \
  --allow-build=sharp \
  --allow-build=tree-sitter-cli \
  openlore < /dev/null

for tool in freebuff openspec openlore; do
  command -v "$tool" >/dev/null 2>&1 || {
    echo "[install-ai-sdlc] ERROR: expected command '$tool' is not on PATH" >&2
    exit 1
  }
done

# ---------------------------------------------------------- 4. Project setup --
# OpenSpec: update the existing project, or initialize (non-interactively) a
# new one so the script also works when copied into a fresh repository.
if [ -d openspec/ ]; then
  openspec update
else
  openspec init --tools all
fi

# OpenLore: .openlore/config.json is committed, so init only when it is absent
# and never overwrite it.
if [ ! -f .openlore/config.json ]; then
  echo "[install-ai-sdlc] Initializing OpenLore (missing .openlore/config.json)..."
  openlore init
fi

# ARM64 Linux (Apple-Silicon devcontainers): tree-sitter-c-sharp@0.21.3 ships
# no linux-arm64 prebuilt binary, so compile the C#/Bash grammar bindings
# before the first analysis (the repair script is the ARM64 fallback/verifier;
# it ships with this repo, so only run it when actually present).
if [ -f scripts/repair-openlore-grammar.sh ] \
  && [ "$(uname -s)" = "Linux" ] \
  && { [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; }; then
  echo "[install-ai-sdlc] ARM64 detected — repairing OpenLore grammars..."
  bash scripts/repair-openlore-grammar.sh
fi

echo "[install-ai-sdlc] Building the OpenLore call-graph index (no API key)..."
openlore analyze

echo "[install-ai-sdlc] Running health checks..."
openlore doctor || echo "[install-ai-sdlc] warning: openlore doctor reported issues (the optional LLM connection line is expected)"
openspec validate --all

echo "[install-ai-sdlc] Optional (recommended): enforce drift checks per commit with:"
echo "[install-ai-sdlc]   openlore drift --install-hook"

# --------------------------------------------------- Optional: AWS tooling ----
# AWS CLI v2 + uv (for the AWS MCP proxy), plus the GitHub CLI and Terraform
# used by the deployment workflows — all user-local.
if [ "$WITH_AWS" = "1" ]; then
  echo "[install-ai-sdlc] Installing AWS tooling (AWS CLI v2, uv, gh, terraform)..."
  mkdir -p "$HOME/.local/bin"

  if command -v aws >/dev/null 2>&1; then
    echo "[install-ai-sdlc] AWS CLI already present: $(aws --version 2>&1 | sed -n '1p')"
  else
    echo "[install-ai-sdlc] Installing AWS CLI v2 (user-local, no sudo)..."
    # The current installer is user-local by default (XDG_DATA_HOME/XDG_BIN_HOME
    # default to ~/.local/share and ~/.local/bin) and takes no dir flags.
    curl -fsSL 'https://awscli.amazonaws.com/v2/install.sh' | bash
    echo "[install-ai-sdlc] AWS CLI installed: $(aws --version 2>&1 | sed -n '1p')"
  fi

  if command -v uv >/dev/null 2>&1; then
    echo "[install-ai-sdlc] uv already present: $(uv --version 2>&1)"
  else
    echo "[install-ai-sdlc] Installing uv (provides uvx for the AWS MCP proxy)..."
    curl -LsSf https://astral.sh/uv/install.sh | sh
    echo "[install-ai-sdlc] uv installed: $(uv --version 2>&1)"
  fi

  if command -v gh >/dev/null 2>&1; then
    echo "[install-ai-sdlc] gh already present: $(gh --version 2>&1 | sed -n '1p')"
  else
    echo "[install-ai-sdlc] Installing GitHub CLI (gh)..."
    GH_VERSION="$(curl -fsSL https://api.github.com/repos/cli/cli/releases/latest | sed -n 's/.*"tag_name": *"v\([^"]*\)".*/\1/p')"
    case "$(uname -m)" in
      aarch64|arm64) GH_ARCH=arm64 ;;
      x86_64) GH_ARCH=amd64 ;;
      *) echo "[install-ai-sdlc] Unsupported arch for gh: $(uname -m)" >&2; exit 1 ;;
    esac
    curl -fsSL "https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_${GH_ARCH}.tar.gz" \
      | tar -xz -C /tmp -f -
    cp "/tmp/gh_${GH_VERSION}_linux_${GH_ARCH}/bin/gh" "$HOME/.local/bin/"
    echo "[install-ai-sdlc] gh installed: $(gh --version 2>&1 | sed -n '1p')"
  fi

  if command -v terraform >/dev/null 2>&1; then
    echo "[install-ai-sdlc] terraform already present: $(terraform version 2>&1 | sed -n '1p')"
  else
    echo "[install-ai-sdlc] Installing Terraform 1.9.8 (pinned to the CI/bootstrap version)..."
    case "$(uname -m)" in
      aarch64|arm64) TF_ARCH=arm64 ;;
      x86_64) TF_ARCH=amd64 ;;
      *) echo "[install-ai-sdlc] Unsupported arch for terraform: $(uname -m)" >&2; exit 1 ;;
    esac
    curl -fsSL -o /tmp/terraform.zip \
      "https://releases.hashicorp.com/terraform/1.9.8/terraform_1.9.8_linux_${TF_ARCH}.zip"
    unzip -o -q /tmp/terraform.zip -d "$HOME/.local/bin/"
    echo "[install-ai-sdlc] terraform installed: $(terraform version 2>&1 | sed -n '1p')"
  fi

  # Pre-warm the MCP proxy package so the first AWS MCP connection does not stall
  if command -v uvx >/dev/null 2>&1; then
    echo "[install-ai-sdlc] Pre-caching AWS MCP proxy (mcp-proxy-for-aws)..."
    uvx mcp-proxy-for-aws==1.6.4 --help >/dev/null 2>&1 || true
  fi
fi

# ------------------------------------------- Persist the setup for new shells --
rc_append '# --- AI SDLC toolchain (install-ai-sdlc.sh) ---
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
export PNPM_HOME="'"$PNPM_HOME"'"
export PATH="$PNPM_HOME/bin:$HOME/.local/bin:$PATH"'

echo "[install-ai-sdlc] Done: freebuff, openspec and openlore are ready."
if [ "$WITH_AWS" = "1" ]; then
  echo "[install-ai-sdlc] After a devcontainer rebuild, re-authenticate once with:"
  echo "[install-ai-sdlc]   aws login --remote --region eu-west-3   (or plain 'aws login' where a browser can reach this device)"
  echo "[install-ai-sdlc] Credentials are valid 12h and renewable 90 days without re-authenticating."
fi
