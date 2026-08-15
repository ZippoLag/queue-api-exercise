## 1. VS Code wiring

- [ ] 1.1 Add `.vscode/tasks.json` with compose orchestration tasks: `compose: up` (prod-like), `compose: up (debug)` (`-f docker-compose.yml -f docker-compose.dev.yml`), `compose: down`, `compose: reset` (`down -v`)
- [ ] 1.2 Add `.vscode/launch.json` with a compound `Both APIs (host)` profile (the two `launchSettings` `http` profiles) and `Attach: CmsWebhook (container)` / `Attach: Users (container)` profiles targeting the debug-mode containers

## 2. Container debug mode

- [ ] 2.1 Add `docker-compose.dev.yml` — SDK-image + repo bind-mount per API (reusing the `init` pattern), `dotnet watch` Debug runs, same host ports `5264`/`5265`, same `queue-db` volume and `ConnectionStrings__*` env vars, `init` ordering preserved (`service_completed_successfully`, `cms-api` before `users-api`); file header documents why it is explicit rather than `.override.yml`
- [ ] 2.2 Verify the default stack is unchanged: `docker compose up` still builds and runs the production Release images (no watch/debug behavior)
- [ ] 2.3 Verify debug mode (requires Docker on the host): the documented `-f` command boots both APIs from source with hot reload against the seeded shared stores; a source edit reloads; the editor can attach to a container process

## 3. Documentation

- [ ] 3.1 Add a README "Debugging" section — the three surfaces (host F5, devcontainer, containers) with when to use each, the exact commands, and the two traps (host-port collision between the stack and host launches; stores per mode: `db/` vs the `queue-db` volume)
- [ ] 3.2 Add the debugging conventions to `docs/development-style.md` (surface decision tree, why the override is explicit, attach-vs-hot-reload baseline)
- [ ] 3.3 Add a short "Debugging in containers" note to `docs/configuration.md` linking to the README section

## 4. Validation

- [ ] 4.1 Run `openspec validate --all` — the change and existing specs all pass
- [ ] 4.2 Build + full test suite sanity (no application code changes; confirms nothing regressed)
