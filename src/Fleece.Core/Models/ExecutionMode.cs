namespace Fleece.Core.Models;

/// <summary>
/// Defines how child issues should be executed relative to each other.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Children must be completed in sort order (by priority, then title).
    /// Only the first incomplete child can be worked on at a time.
    /// </summary>
    Series,

    /// <summary>
    /// Children can be worked on simultaneously.
    /// All children are actionable as long as their own dependencies are met.
    /// </summary>
    Parallel
}
