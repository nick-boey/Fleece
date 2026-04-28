using Fleece.Core.Models;
using Fleece.Core.Models.Graph;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Fleece-specific adapter that composes the generic <see cref="IGraphLayoutService"/> with
/// Fleece's filtering, ancestor-context, and root-finding rules.
/// </summary>
public interface IIssueLayoutService
{
    /// <summary>
    /// Lays out the active subset of <paramref name="issues"/> as a task graph (mirrors classic <c>list --tree</c>).
    /// </summary>
    GraphLayout<Issue> LayoutForTree(
        IReadOnlyList<Issue> issues,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null);

    /// <summary>
    /// Lays out a filtered task graph for <c>list --next</c>. When <paramref name="matchedIds"/> is non-null,
    /// only the matched issues plus their ancestors are included.
    /// </summary>
    GraphLayout<Issue> LayoutForNext(
        IReadOnlyList<Issue> issues,
        IReadOnlySet<string>? matchedIds = null,
        InactiveVisibility visibility = InactiveVisibility.Hide,
        string? assignedTo = null,
        GraphSortConfig? sort = null);
}
