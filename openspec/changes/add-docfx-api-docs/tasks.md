## 1. Tooling

- [x] 1.1 Add `.config/dotnet-tools.json` and install DocFX 2.78.5 (version-pinned)
- [x] 1.2 Create `docfx.json`: metadata from `src/**/*.csproj` into `api/` (tests/tools excluded structurally), conceptual from `docs/**/*.md` and `README.md` (excluding `docs/archived/**`), resource globs for `docs/**/*.png` and `*.drawio`, output `_site/` — plus a root `toc.yml`/`index.md` landing page (without it the site had no home page and would 404 at the Pages root)

## 2. Local build

- [x] 2.1 Run `docfx build` locally and verify the site renders the API reference and the conceptual Markdown — 47 HTML pages (40 API + landing + docs), 0 warnings/errors; recipe is `dotnet docfx metadata` then `dotnet docfx build` (DocFX 2.78.5 does not auto-run metadata; `docfx metadata --force` crashes with exit 255 — use plain `docfx metadata`)
- [x] 2.2 Add `_site/` and `api/` to `.gitignore` and remove the stale untracked `_site/` and empty `api/` directories at the repository root

## 3. Deploy

- [x] 3.1 Add `.github/workflows/docs.yml` triggered on `main`: setup-dotnet, `dotnet tool restore`, `dotnet docfx metadata` + `dotnet docfx build`, then `configure-pages` → `upload-pages-artifact` (`_site`) → `deploy-pages`, with `pages: write` + `id-token: write` permissions and a `github-pages` environment
- [x] 3.2 Set the repository Pages source to "GitHub Actions" (one-time setting) and verify the first deploy publishes to `https://ZippoLag.github.io/queue-api-exercise/` — user saved the setting in Settings → Pages (confirmed via authenticated Pages API: `build_type: "workflow"`, source `main`/`/`); re-ran the previously failed Docs run (#31810956903) which then passed end-to-end; site verified live: `index.html` 200, docs pages and `api/CmsWebhook.Api.Endpoints.html` 200

## 4. Documentation

- [x] 4.1 Update `README.md` and a docs index pointing at the published site, noting that the canonical Markdown and specs remain the agent-facing sources
