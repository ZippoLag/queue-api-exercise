## 1. Baseline and local gates

- [x] 1.1 Baseline re-verified and corrected during implementation: the review-time 75.5% (1226/1624) was race-inflated; the deterministic floor is 74.82% (1215/1624, ten consecutive clean runs) after fixing the two tests that disposed factories mid-processing (see design decision 4)
- [x] 1.2 Add `global.json` pinning the .NET 9 SDK (`rollForward: latestFeature`)
- [x] 1.3 Add root `Directory.Build.props` with `TreatWarningsAsErrors=true` (verified: the solution already builds with zero warnings under the gate; nothing to fix)
- [x] 1.4 Add `scripts/check-coverage.sh` that aggregates `TestResults/**/coverage.cobertura.xml` (sum `lines-valid`/`lines-covered` across all files) against `.config/coverage-min.txt`, and commit the threshold at 74.5% (deterministic floor 74.82% minus margin — see design decision 4)

## 2. CI workflow

- [x] 2.1 Create `.github/workflows/ci.yml`: checkout, setup-dotnet, restore, build, test with coverage collection, coverage gate, `openspec validate` (pinned CLI)
- [x] 2.2 Verify the workflow passes on a push or pull request and document how to reproduce the checks locally (every workflow step verified locally in a clean copy; GitHub-side verification happens on the first real push — README documents the local reproduction steps)

## 3. Documentation

- [x] 3.1 Update `README.md` (CI status section) and `docs/development-style.md` describing the quality gates and how to raise the coverage threshold
