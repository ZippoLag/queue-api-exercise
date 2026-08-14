## 1. Baseline and local gates

- [ ] 1.1 Baseline already measured (75.5% aggregate line coverage, 1226/1624 across the six test projects) — re-verify on the first CI run as a sanity check
- [ ] 1.2 Add `global.json` pinning the .NET 9 SDK (`rollForward: latestFeature`)
- [ ] 1.3 Add root `Directory.Build.props` with `TreatWarningsAsErrors=true` (verified: the solution already builds with zero warnings under the gate; nothing to fix)
- [ ] 1.4 Add `scripts/check-coverage.sh` that aggregates `TestResults/**/coverage.cobertura.xml` (sum `lines-valid`/`lines-covered` across all files) against `.config/coverage-min.txt`, and commit the threshold at 75.5%

## 2. CI workflow

- [ ] 2.1 Create `.github/workflows/ci.yml`: checkout, setup-dotnet, restore, build, test with coverage collection, coverage gate, `openspec validate` (pinned CLI)
- [ ] 2.2 Verify the workflow passes on a push or pull request and document how to reproduce the checks locally

## 3. Documentation

- [ ] 3.1 Update `README.md` (CI status section) and `docs/development-style.md` describing the quality gates and how to raise the coverage threshold
