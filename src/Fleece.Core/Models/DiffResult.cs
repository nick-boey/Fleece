namespace Fleece.Core.Models;

/// <summary>
/// Represents the result of comparing two JSONL issue files.
/// </summary>
public sealed record DiffResult
{
    /// <summary>
    /// Issues that exist in both files but have different content.
    /// Each tuple contains (File1Version, File2Version).
    /// </summary>
    public required IReadOnlyList<(Issue File1Version, Issue File2Version)> Modified { get; init; }

    /// <summary>
    /// Issues that exist only in the first file.
    /// </summary>
    public required IReadOnlyList<Issue> OnlyInFile1 { get; init; }

    /// <summary>
    /// Issues that exist only in the second file.
    /// </summary>
    public required IReadOnlyList<Issue> OnlyInFile2 { get; init; }

    /// <summary>
    /// Gets the total number of differences found.
    /// </summary>
    public int TotalDifferences => Modified.Count + OnlyInFile1.Count + OnlyInFile2.Count;

    /// <summary>
    /// Gets whether any differences were found.
    /// </summary>
    public bool HasDifferences => TotalDifferences > 0;
}
