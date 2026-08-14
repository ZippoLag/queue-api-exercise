namespace CmsWebhook.Api.Tests;

/// <summary>
/// A temporary directory that removes itself and its contents on dispose.
/// </summary>
/// <remarks>
/// Configuration resolution tests must never touch the repository tree, so every resolved path and
/// secrets/home redirect points into a disposable temp directory.
/// </remarks>
public sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// Creates the temporary directory.
    /// </summary>
    /// <param name="prefix">The directory name prefix.</param>
    public TemporaryDirectory(string prefix)
    {
        FullName = Directory.CreateTempSubdirectory(prefix).FullName;
    }

    /// <summary>
    /// The temporary directory's full path.
    /// </summary>
    public string FullName { get; }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(FullName, recursive: true);
}
