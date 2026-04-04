using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Cli.Commands;

public sealed class OpenCommand(IFleeceService fleeceService)
    : StatusCommandBase(fleeceService)
{
    protected override IssueStatus TargetStatus => IssueStatus.Open;
}

public sealed class ProgressCommand(IFleeceService fleeceService)
    : StatusCommandBase(fleeceService)
{
    protected override IssueStatus TargetStatus => IssueStatus.Progress;
}

public sealed class ReviewCommand(IFleeceService fleeceService)
    : StatusCommandBase(fleeceService)
{
    protected override IssueStatus TargetStatus => IssueStatus.Review;
}

public sealed class CompleteCommand(IFleeceService fleeceService)
    : StatusCommandBase(fleeceService)
{
    protected override IssueStatus TargetStatus => IssueStatus.Complete;
}

public sealed class ArchivedCommand(IFleeceService fleeceService)
    : StatusCommandBase(fleeceService)
{
    protected override IssueStatus TargetStatus => IssueStatus.Archived;
}

public sealed class ClosedCommand(IFleeceService fleeceService)
    : StatusCommandBase(fleeceService)
{
    protected override IssueStatus TargetStatus => IssueStatus.Closed;
}
