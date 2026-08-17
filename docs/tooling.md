# Tooling

**General concept — what the toolchain is.** This repository uses an AI-assisted, spec-driven development toolchain on top of the .NET SDK: [Freebuff](https://github.com/CodebuffAI/freebuff) as the coding assistant, [OpenSpec](https://github.com/Fission-AI/OpenSpec/) as the spec-driven change tracker, and [OpenLore](https://github.com/clay-good/OpenLore) as the static-analysis and drift-tracking tool. The conventions for using them are in [Development style](development-style.md); this page is the install runbook.

**Why this stack.** Freebuff is the coding assistant (kept out of the Dockerfile on purpose — as any automated harness, it should better be run sandboxed); OpenSpec and OpenLore track changes and detect spec/code drift without an API key.

## Installing the tools

The tools install into the devcontainer's terminal via [pnpm](https://pnpm.io/):

```bash
# 1. pnpm (safer alternative to npm)
wget -qO- https://get.pnpm.io/install.sh | ENV="$HOME/.bashrc" SHELL="$(which bash)" bash -
source ~/.bashrc
pnpm runtime set node lts -g

# 2. Global tools
pnpm install -g freebuff              # coding assistant
pnpm install -g @fission-ai/openspec@latest  # OpenSpec CLI (spec-driven development)
pnpm install -g openlore              # OpenLore (static analysis + drift tracking; no API key needed)

# 3. OpenSpec baseline (only when starting a brand-new project; this repo already has openspec/)
[ -d openspec/ ] && openspec update || openspec init

# 4. OpenLore: .openlore/config.json is committed, so just build the index
openlore init      # only needed if .openlore/config.json is missing (e.g. fresh clone without the committed config)
openlore analyze   # builds the call-graph index (no API key; C# is fully supported by openlore >= 2.1.9)

# 5. Health checks
openlore doctor    # every line should be ✓ except the optional "LLM connection" warning (only used by `openlore generate`)
openspec validate --all   # specs must all pass
# openlore verify   # optional: spec/code drift + generation report — REQUIRES an LLM API key (ANTHROPIC_API_KEY/OPENAI_API_KEY/...)

# 6. ARM64 (Apple Silicon) devcontainers only — repair the C#/Bash grammars
# tree-sitter-c-sharp@0.21.3 ships no linux-arm64 prebuilt binary, so C# files
# would be indexed for search but never graphed. Build the native binding from
# source (one-time; idempotent):
bash scripts/repair-openlore-grammar.sh && openlore analyze --force

# 7. Optional but recommended before each commit (no API key)
openlore drift     # detect spec/code drift
openlore drift --install-hook # Enforcing drift check before every commit, use `openlore drift --uninstall-hook` if it starts misbehaving
```

> **Why step 6 only on ARM64.** `tree-sitter-c-sharp@0.21.3` publishes prebuilds for darwin-x64, win32-x64, linux-x64 and darwin-arm64 but not linux-arm64, so Apple-Silicon devcontainers need the one-time source build; without it C# files would be indexed for search but never graphed.

> **Why most of OpenLore is keyless.** `openlore analyze`, `orient`, `drift`, `doctor` and the MCP tools run fully locally; only `openlore verify`/`generate` additionally require an LLM API key.

> The sequence above is also flexible enough to be run in a brand-new project, should the setup be copied elsewhere.

## MCP servers for Freebuff

Freebuff loads MCP servers from `.agents/mcp.json` (searched in the project root, its parent, then `~/.agents/`), keyed by `mcpServers`. This repo wires two servers:

- `openlore` — `openlore mcp --preset full` over stdio (all 73 tools, including the OpenSpec tools `check_spec_drift`, `search_specs`, `get_spec`, `list_spec_domains`, and `audit_spec_coverage`).
- `microsoft-learn` — the official Microsoft Learn MCP over HTTP (referenced in `AGENTS.md`).

**Why the MCP server needs the index first.** The MCP server reads the index built by `openlore analyze`, so steps 4–6 above must run before Freebuff's `orient`/call-graph tools return results on this codebase.

> **Why OpenLore ≥ 2.1.9 is required.** It ships a C#/.NET extractor (methods, constructors, local functions, call edges — verified on this repo: 211 functions across 53 `.cs` files). The only caveat is the native grammar binary noted in step 6.

## See also

- [Development style](development-style.md) — the development approach and conventions for using this toolchain.
- [Testing](testing.md) — the `openspec validate --all` spec-discipline CI gate.
- [AGENTS.md](https://github.com/ZippoLag/queue-api-exercise/blob/main/AGENTS.md) — the operating rules agents follow in this repo.
