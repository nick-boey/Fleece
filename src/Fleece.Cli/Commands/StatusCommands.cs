using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Cli.Commands;

public sealed class OpenCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : StatusCommandBase(issueServiceFactory, storageServiceProvider)
{
    protected override IssueStatus TargetStatus => IssueStatus.Open;
}

public sealed class ProgressCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : StatusCommandBase(issueServiceFactory, storageServiceProvider)
{
    protected override IssueStatus TargetStatus => IssueStatus.Progress;
}

public sealed class ReviewCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : StatusCommandBase(issueServiceFactory, storageServiceProvider)
{
    protected override IssueStatus TargetStatus => IssueStatus.Review;
}

public sealed class CompleteCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : StatusCommandBase(issueServiceFactory, storageServiceProvider)
{
    protected override IssueStatus TargetStatus => IssueStatus.Complete;
}

public sealed class ArchivedCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : StatusCommandBase(issueServiceFactory, storageServiceProvider)
{
    protected override IssueStatus TargetStatus => IssueStatus.Archived;
}

public sealed class ClosedCommand(IIssueServiceFactory issueServiceFactory, IStorageServiceProvider storageServiceProvider)
    : StatusCommandBase(issueServiceFactory, storageServiceProvider)
{
    protected override IssueStatus TargetStatus => IssueStatus.Closed;
}
