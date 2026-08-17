## Context

See proposal.md — Why for the motivation. Current state that shapes the approach:

- The README (324 lines) carries five conceptual domains: quickstart, debugging runbook, AWS deployment runbook, CI/testing internals, docs index.
- `docs/development-style.md` carries four: philosophy, tooling install, quality gates + E2E, debugging conventions.
- Recurring facts (store path, default passwords, reserved users, port-collision/devcontainer warnings, coverage union, `!reset`/Compose version, chown caveat, region default) are replicated across 2–4 files each.
- The OpenSpec specs (8 files) are the machine-readable behavioral source. Per user decision the markdown docs stay **self-contained** — they restate behavior for human readers, but each rule must have exactly **one** prose home.
- `docfx.json` auto-includes `docs/**/*.md` and `README.md` (and excludes `docs/archived/**`); `toc.yml` lists each page explicitly, so new pages need a toc entry.
- `docs/archived/initial_requirements.md` is frozen — never read-modified.
- Cross-links that must be repointed: `docs/configuration.md` → `../README.md#deployment` and `#debugging` (×2), `docs/development-style.md` → `../README.md#debugging`.

## Goals / Non-Goals

**Goals:**
- Each fact has exactly one prose home; other files link to it, never restate it.
- Conceptual domains are not crossed between files.
- Every topic section follows one template: general concept → in-this-project → why → diagram → sample/commands → see-also.
- README becomes a condensed pointer hub; runbooks move to dedicated files.
- Voice neutralized (no first-person narrative); reasoning is preserved as explicit "why".
- The glossary becomes the single place that wrangles naming discrepancies as synonyms.

**Non-Goals:**
- No behavior, code, test, CI, Terraform, or compose changes.
- No changes to `docs/archived/initial_requirements.md`; no changes to OpenSpec requirement text.
- Not an author-from-scratch rewrite: content is moved, consolidated, neutralized — not re-invented.
- Not renaming code projects, service names, or the `cms-webhook` username.
- Command blocks may embed values (ports, passwords, paths) they need to be executable — that is not "prose replication".

## Decisions

### D1 — Target file set and per-file responsibility

| File | Responsibility | Domains it may cover |
|---|---|---|
| `README.md` | Identity, quickstart, pointer hub, docs index | getting started only |
| `docs/architecture.md` | System overview, design decisions ("why"), self-contained behavior (auth, persistence, event processing, users API) | architecture + behavior |
| `docs/configuration.md` | Env chain, precedence, secrets, DB base path, credential store, container env pattern, TLS | configuration |
| `docs/development-style.md` | Development approach, working conventions, code conventions | how we work |
| `docs/dsl_glossary.md` | Domain terms + the naming/synonym table | terminology |
| `docs/debugging.md` **(new)** | The three debugging surfaces, store model, port-collision/devcontainer traps, attach profiles, Linux chown | debugging runbook |
| `docs/testing.md` **(new)** | Test layout, TDD loop, coverage ratchet, E2E suite, reproducing CI locally | testing |
| `docs/tooling.md` **(new)** | Freebuff/OpenSpec/OpenLore install, MCP wiring, ARM64 grammar repair, drift hook | tooling setup |
| `docs/deployment-aws.md` **(new)** | Full AWS runbook: bootstrap, topology, cost, secrets, deploy/rollback/teardown, manual ops | AWS deployment |

`index.md` stays the DocFX landing page but its intro condenses to a link to the README.

### D2 — Source-section mapping (where existing content moves)

**README →**
- Debugging section → `docs/debugging.md`
- Deployment section (bootstrap, topology, cost, deploy, secrets, manual ops) → `docs/deployment-aws.md`
- Continuous Integration section (gates, coverage ratchet, reproduce locally) → `docs/testing.md`
- Quickstart + Using/Testing the APIs stay (condensed); the docs index stays (links updated)

**development-style.md →**
- Tooling install + MCP servers → `docs/tooling.md`
- Quality gates, coverage ratchet, E2E → `docs/testing.md` (merged with README's CI content)
- Debugging conventions → `docs/debugging.md`
- Development approach + AI assistance stay (neutralized)

**configuration.md →**
- "Running in containers": keep the env-pattern part (volume layout, `ASPNETCORE_URLS` binding, init ordering — genuinely configuration); the debug-override explanation links to `docs/debugging.md`
- AWS bullet in Secrets guidance → replace with a link to `docs/deployment-aws.md`

**architecture.md →**
- Behavior stays (self-contained per decision); voice neutralized; misnumbered list fixed; floating notes ("Caching is out of scope", "no signature verification") gain their rationale; add a short "Design decisions" subsection for the architect reader.

### D3 — Fact-ownership table (each fact's single prose home)

| Fact | Single home |
|---|---|
| Store path `src/CmsWebhook/CmsWebhook.Api/db/` | configuration.md → Database base directory |
| Local-development default passwords | configuration.md → Credential store (single canonical listing); README quickstart links to it |
| Reserved users (`cms-webhook`, `administrator`, `regular-user`) | dsl_glossary.md (canonical definitions); configuration.md describes seeding and links |
| Username length rule [10,20] | architecture.md → auth |
| Ports 5264/5265 (dev contract) | README.md → quickstart; command blocks elsewhere may embed them |
| "Only one surface at a time" / port collision | debugging.md |
| Devcontainer has no Docker daemon; devcontainer port forwarding caveat | debugging.md |
| Store-sharing model (dev surfaces share `db/`; prod stack uses `queue-db` volume) | debugging.md |
| `!reset` merge tag / Compose ≥ 2.24.4 | debugging.md → debug containers |
| Linux `chown` caveat | debugging.md → Linux hosts |
| Coverage union semantics + threshold 100.0 | testing.md → coverage ratchet |
| CI gate table | testing.md |
| Region default `eu-west-3` | deployment-aws.md → bootstrap |
| GitHub secrets/vars table | deployment-aws.md → CI deploy |
| Password rotation / Caddy / EBS / stop-start / downgrade | deployment-aws.md → manual ops |
| TLS requirement | configuration.md → TLS |
| `ASPNETCORE_URLS` 0.0.0.0:8080 binding | configuration.md → containers |
| Fail-fast startup rules; Scalar/OpenAPI anonymous | architecture.md (behavior) |

### D4 — Naming and synonyms (wrangled in the glossary)

| Term | Context / when to use | Notes |
|---|---|---|
| `cms-webhook` | the reserved username, and only that | literal string; never renamed |
| `CmsWebhook` | C# project/type names (`CmsWebhook.Api`, `CmsWebhook.Domain`) | CamelCase per C# convention |
| `CMS Webhook` / `CMS Webhook API` | human-facing prose titles | preferred spelling in docs prose |
| `CMSWebhook` | legacy glossary spelling | documented as a synonym of "CMS Webhook"; not used for new prose |
| `Users API` | human-facing prose | "User API" is not used |
| `CmsEntity`, `CmsEvent`, `CmsRequest`, `CmsEventLog`, `Outbox`, `Outbox worker` | domain terms | canonical definitions in glossary |

Slightly different spellings are kept on purpose (they aid text search and map to the correct context); the glossary's synonym table is the single place that reconciles them.

### D5 — Per-topic section template

Every topic section follows this order (omitting parts that don't apply):

1. **General concept** — the general/technical background (e.g., what an outbox / Basic Auth / TLS is), only when a reader might lack it; link to an authoritative external source.
2. **In this project** — how this project applies it: names, paths, specifics.
3. **Why** — the reasoning / business rule / decision, in neutral voice.
4. **Diagram** — ASCII or image where it clarifies.
5. **Example / commands** — copy-pasteable sample code or instructions.
6. **See also** — links to lower-level or sibling files; never restate them.

### D6 — Link convention

- Relative markdown links between files (`../README.md#quickstart`, `testing.md`, `configuration.md#credential-store`).
- Higher-level files condense and link down; lower-level files may link up but never restate up-level content.
- A fact's prose lives in exactly one file; anywhere else that needs it links to that home. Command blocks may embed the values they execute.

### D7 — Voice neutralization

- First-person narrative → neutral, preserving the reasoning. Examples:
  - "it would be an oversight in my years of experience to not treat this project as if it had plans to grow" → "the project is treated as if it will grow, so boundaries stay clean from the start".
  - "I've chosen to tackle this as if it was a requirement coming from a client" → "the requirements are taken at face value, as from a client".
  - "I'm simplifying by ignoring these scenarios" → "these scenarios are intentionally simplified out of scope" (and placed with the relevant decision).
- Unresolved markers such as "(and simple UI?)" are removed or converted to an explicit "Out of scope" note.

### D8 — DocFX handling

- `docfx.json` already includes `docs/**/*.md` → new files render with no config change; `docs/archived/**` stays excluded.
- `toc.yml` gains entries for debugging, testing, tooling, deployment-aws (order decided at apply time).
- `index.md` intro condenses to link to the README instead of duplicating it.

### D9 — AGENTS.md documentation-maintenance rules

`AGENTS.md` gains a **Documentation maintenance** section (placed after "Project Structure") so future agents keep `README.md` and `docs/**` aligned with the rules below. It encodes: one-fact-one-home (no prose replication; command blocks may embed values), the per-topic template, a file-responsibility table (below), the naming/synonym policy (D4), neutral voice, and the archived-file invariant. Drafted text:

```markdown
## Documentation

`README.md` and `docs/**` are the canonical human documentation (the DocFX site is a generated view — never a separate copy). Keep them up to date whenever behavior, configuration, or tooling changes, and update `toc.yml` and the README docs index when pages are added, moved, or removed.

- **One fact, one home**: never replicate prose across files. Each fact lives in exactly one file; other files link to it (relative markdown links). Command blocks may embed the values they execute (ports, passwords, paths) — that is not replication.
- **Per-topic template**: every topic section separates general concept → in-this-project specifics → why (reasoning) → diagram → sample/commands → see-also links.
- **Domains don't cross files**: each file owns its conceptual domain (table below); higher-level files condense and link down to lower-level runbooks instead of restating them.
- **Naming**: context-appropriate spellings are intentional — `cms-webhook` is the reserved username (never renamed), `CmsWebhook` is the C# naming, `CMS Webhook` is the prose title. `docs/dsl_glossary.md` is the single place that reconciles them as synonyms.
- **Voice**: neutral third person; reasoning is written as an explicit "why" next to the fact it justifies.
- **Archived invariant**: never modify `docs/archived/initial_requirements.md`.

| File | Owns |
|---|---|
| `README.md` | identity, quickstart, pointer hub, docs index |
| `docs/architecture.md` | system overview, design decisions, behavior |
| `docs/configuration.md` | configuration chain, secrets, DB paths, TLS |
| `docs/development-style.md` | development approach and conventions |
| `docs/dsl_glossary.md` | terminology + naming synonyms |
| `docs/debugging.md` | debugging surfaces runbook |
| `docs/testing.md` | testing, coverage ratchet, CI gates |
| `docs/tooling.md` | tooling setup |
| `docs/deployment-aws.md` | AWS deployment runbook |
```

The table is the same one as D1 — AGENTS.md becomes the standing home of the file-responsibility map so it survives beyond this change's apply phase.

## Risks / Trade-offs

- **External links to README anchors break** (GitHub deep links to `#debugging`, `#deployment`, `#continuous-integration`) → keep those stable top-level headings in the README even as their bodies shrink to a summary + link, so the anchors still resolve.
- **Large mechanical move loses content** → implement file-by-file (copy → edit → delete old), then verify with a link grep and a full local CI-equivalent run.
- **Docs/specs behavior duplication persists** (per user decision) → accepted; architecture.md adds a pointer that the OpenSpec specs are the machine-readable twin, and each rule is stated in exactly one docs file.
- **DocFX nav misses new pages** → explicit toc.yml task in tasks.md.
- **Voice neutralization reads as rewriting author intent** → only the personal narrative is removed; every decision and its "why" is preserved.

## Migration Plan

1. Create the four new files by moving content (copy → edit → delete the old sections).
2. Rewrite the README to pointer-hub form, keeping stable top-level anchors.
3. Edit architecture.md, configuration.md, development-style.md, dsl_glossary.md (neutralize, dedupe, add synonym table).
4. Update toc.yml, index.md, AGENTS.md, and all cross-links (including the three `../README.md#...` references).
5. Verify: grep for dangling relative links; `openspec validate --all`; local CI-equivalent (`dotnet build` + `dotnet test` + coverage) to prove no code changed; `dotnet docfx build` if the tool is available.
6. Rollback: `git revert` (docs-only change).

## Open Questions

- Whether `dotnet docfx` is available locally to verify the site build — deferrable to apply.
- Exact `toc.yml` ordering and section titles — deferrable to apply.
