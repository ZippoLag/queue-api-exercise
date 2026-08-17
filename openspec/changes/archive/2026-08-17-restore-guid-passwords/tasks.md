## 1. Enforce the GUID rule in the init tool

- [x] 1.1 Add GUID validation to `tools/AuthDbInit/Cli.cs`: reject any of the three passwords that is not a dashed 8-4-4-4-12 GUID (`Guid.TryParseExact(password, "D")`) with a descriptive error and exit code 1
- [x] 1.2 Add unit tests in `tools/AuthDbInit.Tests/CliTests.cs`: non-GUID passwords are rejected with the descriptive error; dashed GUID passwords still seed successfully

## 2. Make AWS password generation produce GUIDs

- [x] 2.1 Update `scripts/bootstrap-aws.sh` to generate dashed GUIDs (reuse `openssl rand -hex 16` output formatted as 8-4-4-4-12) instead of bare hex
- [x] 2.2 Update the password-rotation command in `docs/deployment-aws.md` to generate a dashed GUID the same way

## 3. Align documentation

- [x] 3.1 Update `docs/configuration.md` credential-store section: passwords must be randomly generated dashed GUIDs; the init tool rejects other formats; deployed (SSM) passwords are GUIDs

## 4. Rotate the deployed demo credentials

- [x] 4.1 Write three new dashed GUIDs to SSM (`/queue-api/demo/{cms,admin,regular}-password`), delete the node store, redeploy via `scripts/deploy-aws.sh`, and verify the smoke flow authenticates with the new GUIDs
