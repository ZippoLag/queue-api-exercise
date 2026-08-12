## 1. Auth library: document the store-unavailable exception

- [x] 1.1 In `IUserCredentialsProvider.cs`, add `<exception cref="InvalidOperationException">` to `VerifyCredentialsAsync` and `UserExistsAsync` describing the store-unavailable failure, keeping the `<remarks>` for the why
- [x] 1.2 Confirm `DbUserCredentialsProvider` inherits the tags via `<inheritdoc/>` and that no prose exception description remains in its member docs

## 2. Init tool: document the store failure

- [x] 2.1 In `AuthDbInitializer.InitializeAsync`, add `<exception cref="System.Data.Common.DbException">` covering store-unreachable failures during schema creation or seeding

## 3. API: XML docs on the Program.cs helpers

- [x] 3.1 Convert `ResolveConnectionString`'s prose comment to a `<summary>/<param>/<returns>` block with `<exception cref="InvalidOperationException">` for a missing `ConnectionStrings:AuthDb`
- [x] 3.2 Convert `ResolveCmsUsername`'s prose comment to a `<summary>/<param>/<returns>` block with `<exception cref="InvalidOperationException">` for an out-of-range username length
- [x] 3.3 Convert `FindRepositoryRoot`'s prose comment to a `<summary>/<param>/<returns>` block documenting the `null` return (no exception tag)

## 4. Validation

- [x] 4.1 `dotnet build` succeeds with 0 warnings (XML doc tag shape is validated by the compiler)
- [x] 4.2 `dotnet test` passes the full suite
- [x] 4.3 Grep confirms no prose "Throws …" lines remain in the touched files
