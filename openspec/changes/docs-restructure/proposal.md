## Why

The documentation has grown convoluted: the README is five documents in one (quickstart, debugging runbook, AWS deployment runbook, CI internals, docs index), `docs/development-style.md` mixes philosophy with tooling installs and testing internals, and the same facts (store paths, default passwords, port-collision warnings, coverage mechanics, reserved users) are replicated across 2–4 files each, so they drift and stale. A first-time reader (new developer, tester, architect, product owner) cannot tell which surface to use, which document owns a fact, or whether a rule is current.

## What Changes

- **README.md** shrinks to: project identity, quickstart (Docker Compose + manual, condensed), a short "how to debug / test / deploy" pointer hub, and the docs index. All runbooks move out.
- **New `docs/debugging.md`** — the three debugging surfaces (host / devcontainer / debug containers), the one-at-a-time rule, the store-sharing model, port-collision and devcontainer-port caveats, attach profiles, Linux chown caveat. Consolidates README's Debugging section + development-style's debugging conventions.
- **New `docs/testing.md`** — test project layout, the TDD loop (write test → run → coverage gate), the coverage ratchet mechanics, the E2E suite and its conventions, and how to reproduce CI checks locally. Consolidates README's CI/coverage content + development-style's quality-gate/E2E content.
- **New `docs/tooling.md`** — Freebuff/OpenSpec/OpenLore installation runbook, MCP server wiring, ARM64 grammar repair, drift hook. Consolidates development-style's tooling section.
- **New `docs/deployment-aws.md`** — the full AWS runbook: bootstrap, topology, cost, GitHub secrets, manual deploy/rollback/teardown, password rotation, Caddy ops, EBS snapshots, stop/start, t4g downgrade. Consolidates README's Deployment section + configuration.md's AWS bullet.
- **`docs/architecture.md`** — voice neutralized (first-person diary removed), misnumbered list fixed, floating one-liners ("Caching is out of scope", "no signature verification") gain their rationale, and the behavioral descriptions stay self-contained (docs remain readable without the specs). A design-decisions summary section is added for the architect reader.
- **`docs/configuration.md`** — keeps the environment chain, precedence, secrets guidance, DB base path, credential store, TLS. Container and AWS content is either kept only where it is genuinely configuration (container env pattern, `ASPNETCORE_URLS` binding) or replaced by links to the new files.
- **`docs/development-style.md`** — keeps only the development approach and working conventions (philosophy, AI-assistance stance, code conventions), neutralized.
- **`docs/dsl_glossary.md`** — becomes the single place that wrangles naming discrepancies as **synonyms** (per decision): `CmsWebhook` (C# project/type context), `CMS Webhook` / `CMS Webhook API` (human-facing titles), `CMSWebhook` (legacy glossary spelling), `cms-webhook` (the reserved username, never renamed). Typos fixed, near-duplicate entries consolidated.
- **`index.md`** (DocFX landing) — links updated; intro condensed to link to the README instead of duplicating it.
- **`AGENTS.md`** — gains a standing **Documentation maintenance** section encoding the documentation rules (one-fact-one-home, per-topic template, file-responsibility table, naming/synonym policy, neutral voice, archived-file invariant), so future work keeps `README.md` and `docs/**` up to date and consistent.
- **Cross-references updated** wherever files pointed at README anchors that no longer exist (`../README.md#deployment`, `#debugging`, `#continuous-integration`).
- Every fact lives in exactly one file; other files link to it. Zero new replication.

## Capabilities

### New Capabilities

None — this change alters no system behavior. It is a documentation restructuring.

### Modified Capabilities

None — no OpenSpec requirement text changes. The `developer-debugging` spec lists `README.md`, `docs/development-style.md`, `docs/configuration.md` among its source files; those file references may be updated to the new paths during apply, but no requirement or scenario changes.

Per the schema rules, `.openspec.yaml` sets `skip_specs: true` (pure docs change; a spec delta is not invented to satisfy validation).

## Impact

- **Files rewritten**: `README.md`, `docs/architecture.md`, `docs/configuration.md`, `docs/development-style.md`, `docs/dsl_glossary.md`, `index.md`.
- **Files created**: `docs/debugging.md`, `docs/testing.md`, `docs/tooling.md`, `docs/deployment-aws.md`.
- **Files touched (links only)**: `AGENTS.md` (adds the Documentation maintenance section + reference check), `openspec/specs/developer-debugging/spec.md` (source-file list), `scripts/init-db.sh` (comment reference — likely unchanged, verify).
- **Never modified**: `docs/archived/initial_requirements.md` (hard constraint).
- **No impact**: source code, tests, CI workflows, Terraform, compose files, ports, behavior. The DocFX site re-renders from the same markdown sources on push; no DocFX config change expected beyond what `index.md` needs.
