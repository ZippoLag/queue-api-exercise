# Tooling

**General concept — what the toolchain is.** This repository uses an AI-assisted, spec-driven development toolchain on top of the .NET SDK: [Freebuff](https://github.com/CodebuffAI/freebuff) as the coding assistant, [OpenSpec](https://github.com/Fission-AI/OpenSpec/) as the spec-driven change tracker, and [OpenLore](https://github.com/clay-good/OpenLore) as the static-analysis and drift-tracking tool. The conventions for using them are in [Development style](development-style.md); this page is the install runbook.

**Why this stack.** Freebuff is the coding assistant (kept out of the Dockerfile on purpose — as any automated harness, it should better be run sandboxed); OpenSpec and OpenLore track changes and detect spec/code drift without an API key.

## Installing the tools

The whole toolchain installs with one idempotent, user-local command — [`scripts/install-ai-sdlc.sh`](../scripts/install-ai-sdlc.sh) (its executable steps; keep them in sync):

```bash
bash scripts/install-ai-sdlc.sh          # pnpm, Freebuff, OpenSpec, OpenLore + AWS CLI/uv
bash scripts/install-ai-sdlc.sh --skip-aws        # AI tooling only
bash scripts/install-ai-sdlc.sh --with-drift-hook # also install the `openlore drift` pre-commit hook
```

It installs [pnpm](https://pnpm.io/), the global tools ([Freebuff](https://github.com/CodebuffAI/freebuff), [OpenSpec](https://github.com/Fission-AI/OpenSpec/), [OpenLore](https://github.com/clay-good/OpenLore)), runs the OpenSpec baseline and `openlore analyze` index build, repairs the OpenLore grammars on ARM64, and installs the AWS tooling (see [AWS tooling](#aws-tooling)). Re-running after a devcontainer rebuild updates the tools and rebuilds the index.

> **Why step 6 only on ARM64.** `tree-sitter-c-sharp@0.21.3` publishes prebuilds for darwin-x64, win32-x64, linux-x64 and darwin-arm64 but not linux-arm64, so Apple-Silicon devcontainers need the one-time source build (the script does it); without it C# files would be indexed for search but never graphed.

> **Why most of OpenLore is keyless.** `openlore analyze`, `orient`, `drift`, `doctor` and the MCP tools run fully locally; only `openlore verify`/`generate` additionally require an LLM API key.

> The script is flexible enough to be run in a brand-new project, should the setup be copied elsewhere.

## AWS tooling

The AWS tooling is installed by the same script as the AI tools (`bash scripts/install-ai-sdlc.sh`; `--skip-aws` to omit it). It installs, user-local and idempotently:

- **AWS CLI v2** — the credential base for everything AWS.
- **uv** — provides `uvx`, which runs the AWS MCP Server SigV4 proxy wired in `.agents/mcp.json`.
- Pre-warms the `mcp-proxy-for-aws` package so the first MCP connection does not stall.

Credentials are never baked in: after a rebuild, run the script and then authenticate once with `aws login --remote --region eu-west-3` (or plain `aws login` where a browser can reach this device). The session is valid 12 hours and renews for 90 days without re-authenticating. For the AWS deployment runbook, see [Deployment](deployment-aws.md).

**Why not the Agent Toolkit wizard.** `aws configure agent-toolkit` auto-detects a fixed set of agents (Claude Code, Codex, Cursor, …) by their global config directories; Freebuff is not on that list, so the wizard refuses to install. The equivalent pieces are wired manually instead: the `aws-mcp` entry below, and AWS skills installed into `.agents/skills/` (see the [AWS guidance](../AGENTS.md)).

## MCP servers for Freebuff

Freebuff loads MCP servers from `.agents/mcp.json` (searched in the project root, its parent, then `~/.agents/`), keyed by `mcpServers`. This repo wires three servers:

- `openlore` — `openlore mcp --preset full` over stdio (all 73 tools, including the OpenSpec tools `check_spec_drift`, `search_specs`, `get_spec`, `list_spec_domains`, and `audit_spec_coverage`).
- `microsoft-learn` — the official Microsoft Learn MCP over HTTP (referenced in `AGENTS.md`).
- `aws-mcp` — the [AWS MCP Server](https://aws-mcp.us-east-1.api.aws/mcp), connected through the SigV4 proxy (`uvx mcp-proxy-for-aws==1.6.4`, metadata `AWS_REGION=eu-west-3`). It signs requests with the `aws login` session credentials (auto-rotating every 15 minutes) and exposes `retrieve_skill`/`search_documentation`, so any skill in the AWS catalog can be pulled on demand without installing it locally.

**Why the MCP server needs the index first.** The MCP server reads the index built by `openlore analyze`, so steps 4–6 above must run before Freebuff's `orient`/call-graph tools return results on this codebase.

> **Why OpenLore ≥ 2.1.9 is required.** It ships a C#/.NET extractor (methods, constructors, local functions, call edges — verified on this repo: 211 functions across 53 `.cs` files). The only caveat is the native grammar binary noted in step 6.

## See also

- [Development style](development-style.md) — the development approach and conventions for using this toolchain.
- [Testing](testing.md) — the `openspec validate --all` spec-discipline CI gate.
- [AGENTS.md](https://github.com/ZippoLag/queue-api-exercise/blob/main/AGENTS.md) — the operating rules agents follow in this repo.
