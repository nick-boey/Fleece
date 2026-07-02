using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Cli.Workflows;

/// <summary>
/// Tracker-neutral seam for the durable-tracker commands (<c>promote</c>/<c>absorb</c>/<c>auth</c>).
/// Exactly one implementation is registered per repository, selected from the configured
/// <c>tracker</c> setting: <see cref="GitHubTrackerWorkflow"/> delegates to the existing
/// <see cref="IGitHubService"/>; <see cref="LinearTrackerWorkflow"/> is CLI-local and makes no
/// network calls. Each method owns its command's tracker-specific output and returns the process
/// exit code.
/// </summary>
public interface ITrackerWorkflow
{
    /// <summary>Escalates a pre-resolved bundle of Fleece issues into the durable tracker.</summary>
    Task<int> PromoteAsync(PromoteContext context, CancellationToken cancellationToken = default);

    /// <summary>Pulls (GitHub) or guides pulling (Linear) a durable issue into Fleece.</summary>
    Task<int> AbsorbAsync(AbsorbContext context, CancellationToken cancellationToken = default);

    /// <summary>Reports authentication status for the active tracker.</summary>
    Task<int> AuthAsync(AuthContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The escalation payload for <c>promote</c>, pre-resolved and filtered by the command: the bundle
/// of issues to promote (at least one, none already promoted), the formatted issue body, the
/// optional external reference (<c>--ref</c>), and whether JSON output was requested. The bundle's
/// first entry is the root whose title heads the created issue.
/// </summary>
public sealed record PromoteContext(
    IReadOnlyList<Issue> Bundle,
    string Body,
    string? Ref,
    bool Json);

/// <summary>The raw reference argument to <c>absorb</c> (e.g. <c>#123</c> or <c>ENG-42</c>).</summary>
public sealed record AbsorbContext(string Reference, bool Json);

/// <summary>Options for <c>auth</c>.</summary>
public sealed record AuthContext(bool Json);

/// <summary>Shared tracker-workflow constants and helpers.</summary>
public static class TrackerWorkflow
{
    /// <summary>The keyed-tag key that marks an issue as promoted to a durable tracker.</summary>
    public const string PromotedTagKey = "promoted";

    /// <summary>
    /// Records a promotion on each bundled issue: sets status <c>Promoted</c> and adds the
    /// <c>promoted=&lt;ref&gt;</c> keyed tag. Shared by every tracker — only the ref differs
    /// (a GitHub issue number's string form vs a Linear identifier/URL).
    /// </summary>
    public static async Task RecordPromotionAsync(
        IFleeceService fleeceService,
        IReadOnlyList<Issue> bundle,
        string reference,
        CancellationToken cancellationToken = default)
    {
        foreach (var issue in bundle)
        {
            var tags = KeyedTag.AddValue(issue.Tags, PromotedTagKey, reference);
            await fleeceService.UpdateAsync(
                id: issue.Id,
                status: IssueStatus.Promoted,
                tags: tags,
                cancellationToken: cancellationToken);
        }
    }
}
