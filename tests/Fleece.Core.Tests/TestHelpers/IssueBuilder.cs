using Fleece.Core.Models;

namespace Fleece.Core.Tests.TestHelpers;

public class IssueBuilder
{
    private string _id = "abc123";
    private string _title = "Test Issue";
    private string? _description;
    private IssueStatus _status = IssueStatus.Open;
    private IssueType _type = IssueType.Task;
    private int? _linkedPr;
    private IReadOnlyList<string> _linkedIssues = [];
    private IReadOnlyList<string> _parentIssues = [];
    private int? _priority;
    private DateTimeOffset _lastUpdate = DateTimeOffset.UtcNow;

    public IssueBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public IssueBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public IssueBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public IssueBuilder WithStatus(IssueStatus status)
    {
        _status = status;
        return this;
    }

    public IssueBuilder WithType(IssueType type)
    {
        _type = type;
        return this;
    }

    public IssueBuilder WithLinkedPr(int? pr)
    {
        _linkedPr = pr;
        return this;
    }

    public IssueBuilder WithLinkedIssues(params string[] issues)
    {
        _linkedIssues = issues;
        return this;
    }

    public IssueBuilder WithParentIssues(params string[] parents)
    {
        _parentIssues = parents;
        return this;
    }

    public IssueBuilder WithPriority(int? priority)
    {
        _priority = priority;
        return this;
    }

    public IssueBuilder WithLastUpdate(DateTimeOffset lastUpdate)
    {
        _lastUpdate = lastUpdate;
        return this;
    }

    public Issue Build() => new()
    {
        Id = _id,
        Title = _title,
        Description = _description,
        Status = _status,
        Type = _type,
        LinkedPR = _linkedPr,
        LinkedIssues = _linkedIssues,
        ParentIssues = _parentIssues,
        Priority = _priority,
        LastUpdate = _lastUpdate
    };
}
