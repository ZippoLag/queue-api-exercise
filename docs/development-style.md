# Development Style

## Development approach
When given an exercise for an interview a common temptation is to over-engineer as a way to "flex" or display prowess, however I've chosen to tackle this as if it was a requirement coming from a client: taking the list of requirements at face value, not over-thinking abstractions and bolting-on external dependencies when they can be avoided.

Regular instinct and "current trends" / "best" practices would have guided me to a "standard" solution of "just" picking up RabbitMQ and/or a host of libraries, however I'm deliberately choosing to keep it as simple as possible at each increment.

Speaking of "increments", I will be developing this solution following TDD as much as possible.

### AI Assistance
I've been encouraged to rely on AI assistance for the production of this solution, however I won't just be delegating the full coding / doing SDD. I prefer to guide Agents one change at a time, and to write relevant text (such as this README) by hand whenever I want my voice to be preserved. Then regarding DSL and "specs", I will take a "code as source of truth" approach, where implementation code and naming conventions will explicitly show the "what" and "how", and always ensuring that Summary comments explaining the "why" are properly present.

#### Installing FREEBUFF and tooling
Due to budget constraints, I'm using [FREEBUFF](https://github.com/CodebuffAI/freebuff) as coding assistant since it's good enough for my purposes. I'm keeping it out of Dockerfile intentionally, but as any other automated harness, it should better be run sandboxed. I'm also using [OpenSpec](https://github.com/Fission-AI/OpenSpec/) and [OpenLore](https://github.com/clay-good/OpenLore) as change trackers, since it's a tool I have been meaning to try and decided this project may be a good chance to test it. I recommend installing these tools within the devcontainer's terminal via [pnpm](https://pnpm.io/) by executing:

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
# NOTE: `openlore drift --install-hook` wrongly detects skill files as drift, run `openlore drift` manually before commit! See https://github.com/clay-good/OpenLore/issues/350
```

### MCP servers for Freebuff
Freebuff loads MCP servers from `.agents/mcp.json` (searched in the project root, its parent, then `~/.agents/`), keyed by `mcpServers`. This repo wires two servers:

- `openlore` — `openlore mcp --preset full` over stdio (all 73 tools, including the OpenSpec tools `check_spec_drift`, `search_specs`, `get_spec`, `list_spec_domains`, and `audit_spec_coverage`).
- `microsoft-learn` — the official Microsoft Learn MCP over HTTP (referenced in `AGENTS.md`).

The MCP server reads the index built by `openlore analyze`, so steps 4–6 above must run before Freebuff's `orient`/call-graph tools return results on this codebase.

> Note: OpenLore ≥ 2.1.9 ships a C#/.NET extractor (methods, constructors, local functions, call edges — verified on this repo: 211 functions across 53 `.cs` files). The only caveat is the native grammar binary: `tree-sitter-c-sharp@0.21.3` publishes prebuilds for darwin-x64, win32-x64, linux-x64 and darwin-arm64 but not linux-arm64, so Apple-Silicon devcontainers need step 6 above once.

> Note: `openlore verify`/`generate` additionally require an LLM API key; everything else (analyze, orient, drift, doctor, MCP tools) is local and keyless.

> Note: I've given the above sequence the flexibility to be ran in a new project, should you want to copy them into your own set-up.
