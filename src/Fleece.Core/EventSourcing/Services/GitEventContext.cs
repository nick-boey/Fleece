using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.EventSourcing.Services;

public sealed class GitEventContext : IEventGitContext
{
    private readonly IGitService _git;
    private string[]? _cachedCommitList;

    public GitEventContext(IGitService git)
    {
        _git = git;
    }

    public string? GetHeadSha()
    {
        var (exitCode, output, _) = _git.RunGitCommand("rev-parse HEAD");
        return exitCode == 0 ? output.Trim() : null;
    }

    public bool IsFileCommittedAtHead(string filePath)
    {
        var (exitCode, _, _) = _git.RunGitCommand($"ls-files --error-unmatch -- \"{filePath}\"");
        return exitCode == 0;
    }

    public int? GetFirstCommitOrdinal(string filePath)
    {
        var (exitCode, shaOutput, _) = _git.RunGitCommand($"log --diff-filter=A --format='%H' -- \"{filePath}\"");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(shaOutput))
        {
            return null;
        }

        var firstSha = shaOutput.Trim().Split('\n')[0].Trim();
        var commits = GetCommitList();
        var index = Array.IndexOf(commits, firstSha);
        return index >= 0 ? index : null;
    }

    private string[] GetCommitList()
    {
        if (_cachedCommitList is not null)
        {
            return _cachedCommitList;
        }

        var (exitCode, output, _) = _git.RunGitCommand("rev-list --reverse HEAD");
        _cachedCommitList = exitCode == 0
            ? output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();
        return _cachedCommitList;
    }
}
