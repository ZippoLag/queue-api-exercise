using System.Text;
using AuthDbInit;
using FluentAssertions;

namespace AuthDbInit.Tests;

/// <summary>
/// Unit tests for <see cref="Cli"/> argument handling, connection-string shaping and output.
/// </summary>
public class CliTests
{
    private const string CmsUsername = "cms-webhook";
    private const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// Verifies a missing argument prints the usage error and exits with code 1.
    /// </summary>
    /// <remarks>
    /// Source business rule: the initialization script requires <c>&lt;db-path&gt; &lt;username&gt; &lt;password&gt;</c>;
    /// running it without all three must fail visibly with usage guidance.
    /// </remarks>
    [Theory]
    [MemberData(nameof(MissingArgumentCases))]
    public async Task RunAsync_WithMissingArguments_PrintsUsageAndFails(string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await Cli.RunAsync(args, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Usage: dotnet run --project tools/AuthDbInit");
        stdout.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a bare db path is wrapped into a SQLite connection string and the created outcome is reported.
    /// </summary>
    /// <remarks>
    /// Source business rule: the tool accepts a plain path and seeds the store (spec scenario
    /// "Initializing a fresh store"); the informational message must describe the created user.
    /// </remarks>
    [Fact]
    public async Task RunAsync_WithBareDbPath_CreatesUserAndReportsCreated()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cli-tests-{Guid.NewGuid():N}.db");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = await Cli.RunAsync(new[] { dbPath, CmsUsername, CmsPassword }, stdout, stderr);

            exitCode.Should().Be(0);
            stderr.ToString().Should().BeEmpty();
            stdout.ToString().Should().Contain($"[Information] Created user '{CmsUsername}' in '{dbPath}'.");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    /// <summary>
    /// Verifies a full connection string is passed through untouched (not wrapped again) and still works.
    /// </summary>
    /// <remarks>
    /// The tool accepts either a bare path or an already-shaped <c>Data Source=...</c> connection string;
    /// a connection string containing '=' must not be double-wrapped into <c>Data Source=Data Source=...</c>.
    /// </remarks>
    [Fact]
    public async Task RunAsync_WithFullConnectionString_PassesItThroughAndCreatesUser()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cli-tests-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath}";
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = await Cli.RunAsync(new[] { connectionString, CmsUsername, CmsPassword }, stdout, stderr);

            exitCode.Should().Be(0);
            stdout.ToString().Should().Contain($"[Information] Created user '{CmsUsername}' in '{connectionString}'.");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    /// <summary>
    /// Verifies re-running on an already-seeded store reports the existing user and exits successfully.
    /// </summary>
    /// <remarks>
    /// Source business rule: the script is idempotent (spec scenario "Re-running the initialization
    /// script"); the second run must not fail and must warn that the user was left unchanged.
    /// </remarks>
    [Fact]
    public async Task RunAsync_WhenUserAlreadyExists_ReportsWarningAndSucceeds()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cli-tests-{Guid.NewGuid():N}.db");
        try
        {
            using var firstStdout = new StringWriter();
            using var firstStderr = new StringWriter();
            var firstRun = await Cli.RunAsync(new[] { dbPath, CmsUsername, CmsPassword }, firstStdout, firstStderr);
            firstRun.Should().Be(0);

            using var secondStdout = new StringWriter();
            using var secondStderr = new StringWriter();
            var secondRun = await Cli.RunAsync(new[] { dbPath, CmsUsername, CmsPassword }, secondStdout, secondStderr);

            secondRun.Should().Be(0);
            secondStderr.ToString().Should().BeEmpty();
            secondStdout.ToString().Should().Contain(
                $"[Warning] User '{CmsUsername}' already exists in '{dbPath}'; leaving it unchanged.");
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    /// <summary>
    /// The argument vectors that omit at least one of <c>&lt;db-path&gt; &lt;username&gt; &lt;password&gt;</c>.
    /// </summary>
    public static IEnumerable<object[]> MissingArgumentCases =>
    [
        [Array.Empty<string>()],
        [new[] { "store.db" }],
        [new[] { "store.db", CmsUsername }],
        [new[] { "", CmsUsername, CmsPassword }],
    ];

    /// <summary>
    /// Verifies the real entry point wires the command line through to <see cref="Cli.RunAsync"/>.
    /// </summary>
    /// <remarks>
    /// The top-level <c>Program.cs</c> shim is one line of glue; invoking the assembly's actual entry
    /// point (not the hoisted class) proves the arg plumbing and exit-code contract end to end, so the
    /// last line of the tool is covered by a meaningful test rather than accepted as untestable glue.
    /// </remarks>
    [Fact]
    public async Task Main_WithValidArguments_SeedsStoreAndReturnsZero()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cli-main-{Guid.NewGuid():N}.db");
        try
        {
            var entryPoint = typeof(Cli).Assembly.EntryPoint
                ?? throw new InvalidOperationException("AuthDbInit entry point not found.");
            var invocation = entryPoint.Invoke(null, new object[] { new[] { dbPath, CmsUsername, CmsPassword } });

            // The compiler may elide the async state machine and emit a synchronous Main, so accept both.
            var exitCode = invocation is int code
                ? code
                : (int)await (Task<int>)invocation!;

            exitCode.Should().Be(0);
            File.Exists(dbPath).Should().BeTrue();
        }
        finally
        {
            DeleteDatabaseFiles(dbPath);
        }
    }

    private static void DeleteDatabaseFiles(string dbPath)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var candidate = dbPath + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
