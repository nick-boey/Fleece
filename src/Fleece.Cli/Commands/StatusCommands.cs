using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Commands;

public sealed class OpenCommand(IFleeceService fleeceService, IAnsiConsole console)
    : StatusCommandBase(fleeceService, console)
{
    protected override IssueStatus TargetStatus => IssueStatus.Open;
}

public sealed class ProgressCommand(IFleeceService fleeceService, IAnsiConsole console)
    : StatusCommandBase(fleeceService, console)
{
    protected override IssueStatus TargetStatus => IssueStatus.Progress;
}

public sealed class ReviewCommand(IFleeceService fleeceService, IAnsiConsole console)
    : StatusCommandBase(fleeceService, console)
{
    protected override IssueStatus TargetStatus => IssueStatus.Review;
}

public sealed class CompleteCommand(IFleeceService fleeceService, IAnsiConsole console)
    : StatusCommandBase(fleeceService, console)
{
    protected override IssueStatus TargetStatus => IssueStatus.Complete;
}

public sealed class ArchivedCommand(IFleeceService fleeceService, IAnsiConsole console)
    : StatusCommandBase(fleeceService, console)
{
    protected override IssueStatus TargetStatus => IssueStatus.Archived;
}

public sealed class ClosedCommand(IFleeceService fleeceService, IAnsiConsole console)
    : StatusCommandBase(fleeceService, console)
{
    protected override IssueStatus TargetStatus => IssueStatus.Closed;
}
