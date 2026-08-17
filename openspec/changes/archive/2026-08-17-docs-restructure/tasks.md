## 1. Create the new documentation files

- [x] 1.1 Create `docs/debugging.md` by moving the README Debugging section and development-style's debugging conventions (three surfaces, store model, port-collision and devcontainer traps, attach profiles, Linux chown caveat) into it, following the per-topic template
- [x] 1.2 Create `docs/testing.md` by moving the README CI/coverage content and development-style's quality-gates + E2E content into it (test layout, TDD loop, coverage ratchet, E2E conventions, reproduce-checks-locally)
- [x] 1.3 Create `docs/tooling.md` by moving development-style's tooling install + MCP servers sections into it (Freebuff/OpenSpec/OpenLore install, MCP wiring, ARM64 grammar repair, drift hook)
- [x] 1.4 Create `docs/deployment-aws.md` by moving the README Deployment section and configuration.md's AWS bullet into it (bootstrap, topology, cost, GitHub secrets, deploy/rollback/teardown, manual operations)
- [x] 1.5 Verify each new file has zero content duplicated with any other file (grep for the moved facts)

## 2. Rewrite the README as a pointer hub

- [x] 2.1 Condense the README to identity + quickstart + "how to debug/test/deploy" links + docs index, keeping stable top-level headings `Debugging`, `Deployment`, `Continuous Integration` so existing anchors still resolve
- [x] 2.2 Remove the duplicated devcontainer/Docker-daemon/port-forwarding warnings and the store-path repetition; link to `docs/debugging.md` and `docs/configuration.md` instead
- [x] 2.3 Point the curl walkthrough's password note at the single canonical listing in `docs/configuration.md` instead of repeating the values
- [x] 2.4 Fix the README typos ("tu", "succesfully", trailing-colon heading)

## 3. Edit the existing docs

- [x] 3.1 `docs/architecture.md`: neutralize first-person voice (D7), fix the duplicated `1.` list numbering, give floating notes ("Caching is out of scope", "no signature verification") their rationale, standardize API naming to "CMS Webhook API", add a short "Design decisions" subsection
- [x] 3.2 `docs/configuration.md`: keep env chain/precedence/secrets/DB path/credential store/TLS; trim the container section to the env-pattern only and link to `docs/debugging.md` for the debug override; replace the AWS bullet with a link to `docs/deployment-aws.md`
- [x] 3.3 `docs/development-style.md`: keep only the development approach and conventions, neutralized; remove tooling/gates/E2E/debugging sections (now in tooling.md/testing.md/debugging.md)
- [x] 3.4 `docs/dsl_glossary.md`: add the naming/synonym table (D4) wrangling `cms-webhook` / `CmsWebhook` / `CMS Webhook` / `CMSWebhook` / `Users API`; consolidate the near-duplicate `CmsWebhook (username)` / `CMSWebhook` entries; fix typos ("intially", "it's", "it's")
- [x] 3.5 Fix development-style.md typos ("becomes starts", "to be ran")

## 4. Update DocFX, AGENTS, and cross-links

- [x] 4.1 Add entries to `toc.yml` for debugging, testing, tooling, deployment-aws
- [x] 4.2 Condense `index.md` to link to the README instead of duplicating its intro; update its links
- [x] 4.3 Add the **Documentation maintenance** section to `AGENTS.md` (after "Project Structure"): one-fact-one-home, per-topic template, file-responsibility table, naming/synonym policy, neutral voice, archived-file invariant (text in design.md D9); update the existing `@docs/...` references if any path changed
- [x] 4.4 Repoint the three `../README.md#...` cross-links in `docs/configuration.md` and `docs/development-style.md` to the new files
- [x] 4.5 Update the `developer-debugging` spec's source-file list if it references moved content paths (requirement text unchanged)

## 5. Verify

- [x] 5.1 Grep for dangling relative links (`README.md#`, `docs/`) across all markdown and fix any that 404
- [x] 5.2 Run `openspec validate --all` (spec discipline gate)
- [x] 5.3 Run the local CI-equivalent (`dotnet build` + `dotnet test` + `scripts/check-coverage.sh`) to prove no code changed
- [x] 5.4 Build the docs site with `dotnet docfx build` if the tool is available; otherwise confirm `docfx.json` covers the new files
- [x] 5.5 Confirm `docs/archived/initial_requirements.md` is byte-identical to before the change
- [x] 5.6 Confirm the `AGENTS.md` Documentation maintenance section matches design.md D9 and the file-responsibility table stays in sync with the actual docs layout
