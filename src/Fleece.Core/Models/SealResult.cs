namespace Fleece.Core.Models;

/// <summary>
/// Outcome of a <c>fleece seal</c> operation.
/// </summary>
public sealed record SealResult
{
    /// <summary>Whether the branch was sealed (archive written and live logs cleared).</summary>
    public required bool Sealed { get; init; }

    /// <summary>
    /// Issues blocking the seal because they are still in an active status
    /// (<c>Open</c>/<c>Progress</c>/<c>Review</c>). Empty on success.
    /// </summary>
    public required IReadOnlyList<Issue> ActiveIssues { get; init; }

    /// <summary>Path to the archive file written on success, or null when nothing was archived.</summary>
    public string? ArchivePath { get; init; }

    /// <summary>Number of live issue logs removed on success.</summary>
    public int RemovedCount { get; init; }
}
