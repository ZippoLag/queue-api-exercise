namespace AuthDbInit;

/// <summary>
/// The command-line front-end of the credential-store initializer tool.
/// </summary>
/// <remarks>
/// Hoisted out of <c>Program.cs</c> top-level statements so the arg-parsing and outcome logic is unit
/// testable; <c>Program.cs</c> remains a thin shim forwarding <c>Console</c> streams and returning the
/// exit code. The tool seeds the three reserved users the APIs expect — <c>cms-webhook</c>,
/// <c>administrator</c> and <c>regular-user</c> — with the passwords supplied as positional arguments
/// (the three-password contract of the users-api-vertical change); it never reads credentials from
/// environment variables. The exit-code contract mirrors the script's usage: <c>0</c> when the store was
/// initialized or already seeded, <c>1</c> on a usage error.
/// </remarks>
public static class Cli
{
    /// <summary>
    /// The reserved username of the CMS client, authorized only on the CMS Webhook API.
    /// </summary>
    public const string CmsUsername = "cms-webhook";

    /// <summary>
    /// The reserved username of the system administrator, authorized to enable/disable entity visibility.
    /// </summary>
    public const string AdministratorUsername = "administrator";

    /// <summary>
    /// The reserved username of a regular user, authorized to list visible entities on the Users API.
    /// </summary>
    public const string RegularUserUsername = "regular-user";

    /// <summary>
    /// Runs the tool with the given arguments and writes progress/errors to the supplied writers.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments: <c>&lt;db-path&gt; &lt;cms-password&gt; &lt;admin-password&gt; &lt;regular-password&gt;</c>.
    /// </param>
    /// <param name="stdout">The stream for informational progress output.</param>
    /// <param name="stderr">The stream for error/usage output.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The process exit code: <c>0</c> on success, <c>1</c> when required arguments are missing.</returns>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken = default)
    {
        var dbPath = args.ElementAtOrDefault(0);
        var cmsPassword = args.ElementAtOrDefault(1);
        var adminPassword = args.ElementAtOrDefault(2);
        var regularPassword = args.ElementAtOrDefault(3);

        if (string.IsNullOrWhiteSpace(dbPath)
            || string.IsNullOrWhiteSpace(cmsPassword)
            || string.IsNullOrWhiteSpace(adminPassword)
            || string.IsNullOrWhiteSpace(regularPassword))
        {
            await stderr.WriteLineAsync(
                "[Error] Usage: dotnet run --project tools/AuthDbInit -- <db-path> <cms-password> <admin-password> <regular-password>");
            return 1;
        }

        // A full connection string (contains '=') is passed through untouched; a bare path is wrapped so
        // callers can hand either form to the tool.
        var connectionString = dbPath.Contains('=') ? dbPath : $"Data Source={dbPath}";

        var results = await AuthDbInitializer.InitializeAsync(
            connectionString,
            new[]
            {
                new UserSeed(CmsUsername, cmsPassword),
                new UserSeed(AdministratorUsername, adminPassword),
                new UserSeed(RegularUserUsername, regularPassword),
            },
            cancellationToken);

        foreach (var result in results)
        {
            if (result.Created)
            {
                await stdout.WriteLineAsync($"[Information] Created user '{result.Username}' in '{dbPath}'.");
            }
            else
            {
                await stdout.WriteLineAsync(
                    $"[Warning] User '{result.Username}' already exists in '{dbPath}'; leaving it unchanged.");
            }
        }

        return 0;
    }
}
