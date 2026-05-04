using Fleece.Core.EventSourcing.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.EventSourcing;

[TestFixture]
public sealed class GitEventContextTests
{
    [Test]
    public void GetFirstCommitOrdinal_ReturnsCorrectOrdinals_ForFilesInDifferentCommits()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args == "rev-list --reverse HEAD")
                {
                    return (0, "sha00001\nsha00002\nsha00003\n", "");
                }
                if (args == "log --diff-filter=A --format='%H' -- \"change_aaa.jsonl\"")
                {
                    return (0, "sha00001\n", "");
                }
                if (args == "log --diff-filter=A --format='%H' -- \"change_ccc.jsonl\"")
                {
                    return (0, "sha00003\n", "");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        var ordA = ctx.GetFirstCommitOrdinal("change_aaa.jsonl");
        var ordC = ctx.GetFirstCommitOrdinal("change_ccc.jsonl");

        ordA.Should().Be(0);
        ordC.Should().Be(2);
    }

    [Test]
    public void GetFirstCommitOrdinal_ReturnsNull_ForUncommittedFile()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args == "rev-list --reverse HEAD")
                {
                    return (0, "sha00001\nsha00002\n", "");
                }
                if (args.StartsWith("log --diff-filter=A"))
                {
                    return (128, "", "fatal: bad revision 'HEAD'");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        var ord = ctx.GetFirstCommitOrdinal("uncommitted_file.jsonl");

        ord.Should().BeNull();
    }

    [Test]
    public void GetFirstCommitOrdinal_ReturnsNull_WhenFileNotInHistory()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args == "rev-list --reverse HEAD")
                {
                    return (0, "sha00001\nsha00002\n", "");
                }
                if (args.StartsWith("log --diff-filter=A"))
                {
                    return (0, "", "");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        var ord = ctx.GetFirstCommitOrdinal("never_added.jsonl");

        ord.Should().BeNull();
    }

    [Test]
    public void GetHeadSha_ReturnsNull_WhenGitFails()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args == "rev-parse HEAD")
                {
                    return (128, "", "fatal: not a git repository");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        ctx.GetHeadSha().Should().BeNull();
    }

    [Test]
    public void GetHeadSha_ReturnsSha_WhenGitSucceeds()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args == "rev-parse HEAD")
                {
                    return (0, "abc123def\n", "");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        ctx.GetHeadSha().Should().Be("abc123def");
    }

    [Test]
    public void IsFileCommittedAtHead_ReturnsFalse_WhenFileUntracked()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args.StartsWith("ls-files --error-unmatch"))
                {
                    return (128, "", "fatal: No such path");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        ctx.IsFileCommittedAtHead("untracked.jsonl").Should().BeFalse();
    }

    [Test]
    public void IsFileCommittedAtHead_ReturnsTrue_WhenFileTracked()
    {
        var stub = new StubGitService
        {
            OnRunGitCommand = args =>
            {
                if (args.StartsWith("ls-files --error-unmatch"))
                {
                    return (0, "tracked.jsonl\n", "");
                }
                return (0, "", "");
            }
        };
        var ctx = new GitEventContext(stub);

        ctx.IsFileCommittedAtHead("tracked.jsonl").Should().BeTrue();
    }

    private sealed class StubGitService : IGitService
    {
        public Func<string, (int, string, string)> OnRunGitCommand = _ => (0, "", "");

        public (int ExitCode, string Output, string Error) RunGitCommand(string arguments)
            => OnRunGitCommand(arguments);

        public bool IsGitAvailable() => true;
        public bool IsGitRepository() => true;
        public bool HasFleeceChanges() => false;
        public GitOperationResult StageFleeceDirectory() => GitOperationResult.Ok();
        public GitOperationResult Commit(string message) => GitOperationResult.Ok();
        public GitOperationResult Push() => GitOperationResult.Ok();
        public GitOperationResult CommitFleeceChanges(string message) => GitOperationResult.Ok();
        public GitOperationResult CommitAndPushFleeceChanges(string message) => GitOperationResult.Ok();
        public string? GetCurrentBranch() => "main";
    }
}
