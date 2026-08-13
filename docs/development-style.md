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
wget -qO- https://get.pnpm.io/install.sh | ENV="$HOME/.bashrc" SHELL="$(which bash)" bash - # Installing pnpm since it's a safer alternative to npm
source ~/.bashrc # Reloading the terminal
pnpm runtime set node lts -g
pnpm install -g freebuff
pnpm install -g @fission-ai/openspec@latest
[ -d openspec/ ] && openspec update || openspec init # If the openspec folder doesn't exist (ie, you're starting a new project, you must initialize first)
pnpm install -g openlore # Installs OpenLore to keep track of development drift and to incorporate manual code changes into the spec if need be
[ -f .openlore/index-bundle.olbundle ] && openlore import .openlore/index-bundle.olbundle && openlore analyze || openlore install # Checks if openlore is already initialized, otherwise does so
openlore doctor # Checks openlore has been correctly initialized
openlore verify # Verifies the current specs' validity
# openlore drift --install-hook # currnely wrongly detects skill files as drift, run `openlore drift` manually before commit! See https://github.com/clay-good/OpenLore/issues/350
```

### MCP servers for Freebuff
Freebuff loads MCP servers from `.agents/mcp.json` (searched in the project root, its parent, then `~/.agents/`), keyed by `mcpServers`. This repo wires two servers:

- `openlore` — `openlore mcp --preset full` over stdio (all 73 tools, including the OpenSpec tools `check_spec_drift`, `search_specs`, `get_spec`, `list_spec_domains`, and `audit_spec_coverage`).
- `microsoft-learn` — the official Microsoft Learn MCP over HTTP (referenced in `AGENTS.md`).

> Note: OpenLore 2.x has no C#/.NET function extractor yet (only TypeScript, Go, Rust, Python), so `orient` and the call-graph tools return no results on this .NET codebase; `search_code` (text) and the spec tools still work. `openlore verify`/`generate` additionally require an LLM API key.

> Note: I've given the above sequence the flexibility to be ran in a new project, should you want to copy them into your own set-up.
