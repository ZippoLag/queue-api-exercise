## 1. Tooling

- [ ] 1.1 Add `.config/dotnet-tools.json` and install DocFX 2.78.5 (version-pinned)
- [ ] 1.2 Create `docfx.json`: metadata from `src/**/*.csproj` into `api/` (tests/tools excluded structurally), conceptual from `docs/**/*.md` and `README.md` (excluding `docs/archived/**`), resource globs for `docs/**/*.png` and `*.drawio`, output `_site/`

## 2. Local build

- [ ] 2.1 Run `docfx build` locally and verify the site renders the API reference and the conceptual Markdown
- [ ] 2.2 Add `_site/` and `api/` to `.gitignore` and remove the stale untracked `_site/` and empty `api/` directories at the repository root

## 3. Deploy

- [ ] 3.1 Add `.github/workflows/docs.yml` triggered on `main`: setup-dotnet, `dotnet tool restore`, `dotnet docfx build`, then `configure-pages` → `upload-pages-artifact` (`_site`) → `deploy-pages`, with `pages: write` + `id-token: write` permissions and a `github-pages` environment
- [ ] 3.2 Set the repository Pages source to "GitHub Actions" (one-time setting) and verify the first deploy publishes to `https://ZippoLag.github.io/queue-api-exercise/`

## 4. Documentation

- [ ] 4.1 Update `README.md` and a docs index pointing at the published site, noting that the canonical Markdown and specs remain the agent-facing sources
