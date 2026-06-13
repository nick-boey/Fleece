using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Implements the <c>fleece seal</c> branch-lifecycle operation: archive the issue set to
/// <c>.fleece/archive/issues_&lt;contenthash&gt;.jsonl</c> and clear the live <c>.fleece/issues/</c>
/// directory, but only when every issue is in an inactive status.
/// </summary>
public interface ISealService
{
    /// <summary>
    /// Seals the branch. Succeeds only when all issues are inactive; otherwise returns a
    /// result whose <see cref="SealResult.ActiveIssues"/> lists the blockers and makes no changes.
    /// An empty issue set is a no-op success.
    /// </summary>
    Task<SealResult> SealAsync(CancellationToken cancellationToken = default);
}
