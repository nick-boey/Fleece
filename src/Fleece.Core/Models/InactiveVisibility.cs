namespace Fleece.Core.Models;

/// <summary>
/// Controls which terminal-status issues are visible in graph generation.
/// </summary>
public enum InactiveVisibility
{
    /// <summary>
    /// Terminal-status issues are excluded from the graph entirely
    /// (unless they are ancestors of active issues needed for hierarchy context).
    /// </summary>
    Hide,

    /// <summary>
    /// Show terminal-status issues only if they have at least one descendant
    /// (at any depth) with an active status (Draft, Open, Progress, Review).
    /// </summary>
    IfHasActiveDescendants,

    /// <summary>
    /// Show all terminal-status issues regardless of whether they have active descendants.
    /// </summary>
    Always
}
