# Tooling

**General concept — what the toolchain is.** This repository uses an AI-assisted, spec-driven development toolchain on top of the .NET SDK: [Freebuff](https://github.com/CodebuffAI/freebuff) as the coding assistant, [OpenSpec](https://github.com/Fission-AI/OpenSpec/) as the spec-driven change tracker, and [OpenLore](https://github.com/clay-good/OpenLore) as the static-analysis and drift-tracking tool. The conventions for using them are in [Development style](development-style.md); this page is the install runbook.

**Why this stack.** Freebuff is the coding assistant (kept out of the Dockerfile on purpose — as any automated harness, it should better be run sandboxed); OpenSpec and OpenLore track changes and detect spec/code drift without an API key.

## Installing the tools

The whole toolchain installs with one idempotent, user-local command — [`scripts/install-ai-sdlc.sh`](../scripts/install-ai-sdlc.sh) (its executable steps; keep them in sync):

```bash
bash scripts/install-ai-sdlc.sh            # nvm/node, pnpm, Freebuff, OpenSpec, OpenLore + project setup
bash scripts/install-ai-sdlc.sh --with-aws # also install AWS CLI v2, uv, gh, terraform (see AWS tooling)
```

It installs **Node via [nvm](https://github.com/nvm-sh/nvm)** (version from the repository's `.nvmrc`), **pnpm**, and the global tools ([Freebuff](https://github.com/CodebuffAI/freebuff), [OpenSpec](https://github.com/Fission-AI/OpenSpec/), [OpenLore](https://github.com/clay-good/OpenLore)); then configures the project — `openspec update` (or non-interactive `openspec init --tools all` in a new project), `openlore init` only when `.openlore/config.json` is absent, the ARM64 grammar repair when applicable, `openlore analyze`, and the health checks. Re-running after a devcontainer rebuild updates the tools and rebuilds the index.

**Why build approvals are explicit.** pnpm 10 and later blocks dependency lifecycle scripts until they are approved. The installer therefore uses non-interactive `pnpm add -g --allow-build=...` flags. Every package in the dependency tree that has a build script must be listed or pnpm falls back to an interactive checkbox prompt, so besides OpenSpec, OpenLore and the C#/Bash grammars this also includes `sharp` and `tree-sitter-cli` (openlore dependencies that ship build scripts). The `openlore` install additionally redirects stdin (`< /dev/null`) so a missed approval fails loudly instead of hanging. Optional grammars for unrelated languages are deliberately not approved.

The devcontainer runs as the `vscode` user and leaves the keep-alive to the Dev Containers CLI rather than baking in an image `ENTRYPOINT`/`CMD`. The CLI always injects its own `/bin/sh` wrapper with a keep-alive loop, so an image-level keep-alive would be ignored anyway and is omitted as dead config.

**Why Node comes from nvm, not pnpm.** nvm's node bundles `npm`/`npx`, which the `openlore drift` pre-commit hook needs (`npx --yes openlore`) — so no extra npm install is required. Keep using **pnpm** for global installs in this repo: npm is never used as a package manager here, only as the npx provider bundled with node.

The installer requires pnpm 10 or newer (the release that introduced `--allow-build`); `get.pnpm.io` always installs the current version, and a too-old existing pnpm fails loudly when the flag is rejected. Required setup and analysis failures stop the script with an error (`set -e`); `openlore doctor` output and the AWS MCP proxy prewarm are explicitly best-effort. A drift pre-commit hook is not installed automatically — after setup, run `openlore drift --install-hook` once if you want commits gated on spec/code drift.

> **Why the grammars build from source on ARM64 Linux.** `tree-sitter-c-sharp@0.21.3` publishes prebuilds for darwin-x64, win32-x64, linux-x64 and darwin-arm64 but not linux-arm64, so on Apple-Silicon devcontainers pnpm's install-time build (via its bundled node-gyp) compiles the C#/Bash grammars from source. node-gyp needs a full Python; the devcontainer installs `python3` (the image's default `python3-minimal` has no stdlib) in `.devcontainer/Dockerfile`. The `scripts/repair-openlore-grammar.sh` call then verifies the grammars load and is the fallback if a build ever fails.

> **Why most of OpenLore is keyless.** `openlore analyze`, `orient`, `drift`, `doctor` and the MCP tools run fully locally; only `openlore verify`/`generate` additionally require an LLM API key.

> The script is flexible enough to be run in a brand-new project, should the setup be copied elsewhere.

## AWS tooling

The AWS tooling is optional and installed by passing `--with-aws` to the same script (`bash scripts/install-ai-sdlc.sh --with-aws`). It installs, user-local and idempotently:

- **AWS CLI v2** — the credential base for everything AWS.
- **uv** — provides `uvx`, which runs the AWS MCP Server SigV4 proxy wired in `.agents/mcp.json`.
- **gh (GitHub CLI)** and **Terraform** — used by the deployment workflows; Terraform is pinned to the version CI and bootstrap use.
- Pre-warms the `mcp-proxy-for-aws` package so the first MCP connection does not stall.

Credentials are never baked in: after a rebuild, run the script and then authenticate once with `aws login --remote --region eu-west-3` (or plain `aws login` where a browser can reach this device). The session is valid 12 hours and renews for 90 days without re-authenticating. For the AWS deployment runbook, see [Deployment](deployment-aws.md).

**Why not the Agent Toolkit wizard.** `aws configure agent-toolkit` auto-detects a fixed set of agents (Claude Code, Codex, Cursor, …) by their global config directories; Freebuff is not on that list, so the wizard refuses to install. The equivalent pieces are wired manually instead: the `aws-mcp` entry below, and AWS skills installed into `.agents/skills/` (see the [AWS guidance](../AGENTS.md)).

## MCP servers for Freebuff

Freebuff loads MCP servers from `.agents/mcp.json` (searched in the project root, its parent, then `~/.agents/`), keyed by `mcpServers`. This repo wires three servers:

- `openlore` — `openlore mcp --preset full` over stdio (all 73 tools, including the OpenSpec tools `check_spec_drift`, `search_specs`, `get_spec`, `list_spec_domains`, and `audit_spec_coverage`).
- `microsoft-learn` — the official Microsoft Learn MCP over HTTP (referenced in `AGENTS.md`).
- `aws-mcp` — the [AWS MCP Server](https://aws-mcp.us-east-1.api.aws/mcp), connected through the SigV4 proxy (`uvx mcp-proxy-for-aws==1.6.4`, metadata `AWS_REGION=eu-west-3`). It signs requests with the `aws login` session credentials (auto-rotating every 15 minutes) and exposes `retrieve_skill`/`search_documentation`, so any skill in the AWS catalog can be pulled on demand without installing it locally.

**Why the MCP server needs the index first.** The MCP server reads the index built by `openlore analyze`, so the OpenLore configuration check, ARM64 grammar repair when applicable, and analysis step must run before Freebuff's `orient`/call-graph tools return results on this codebase.

> **Why OpenLore ≥ 2.1.9 is required.** It ships a C#/.NET extractor (methods, constructors, local functions, call edges — verified on this repo: 318 call-graph functions across the C# files). The only caveat is the native grammar build noted in the ARM64 note above.

## See also

- [Development style](development-style.md) — the development approach and conventions for using this toolchain.
- [Testing](testing.md) — the `openspec validate --all` spec-discipline CI gate.
- [AGENTS.md](https://github.com/ZippoLag/queue-api-exercise/blob/main/AGENTS.md) — the operating rules agents follow in this repo.
