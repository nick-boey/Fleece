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
    private string? _group;
    private string? _assignedTo;
    private string? _createdBy;
    private DateTimeOffset _lastUpdate = DateTimeOffset.UtcNow;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

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

    public IssueBuilder WithGroup(string? group)
    {
        _group = group;
        return this;
    }

    public IssueBuilder WithAssignedTo(string? assignedTo)
    {
        _assignedTo = assignedTo;
        return this;
    }

    public IssueBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public IssueBuilder WithLastUpdate(DateTimeOffset lastUpdate)
    {
        _lastUpdate = lastUpdate;
        return this;
    }

    public IssueBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public Issue Build() => new()
    {
        Id = _id,
        Title = _title,
        TitleLastUpdate = _lastUpdate,
        Description = _description,
        DescriptionLastUpdate = _description is not null ? _lastUpdate : null,
        Status = _status,
        StatusLastUpdate = _lastUpdate,
        Type = _type,
        TypeLastUpdate = _lastUpdate,
        LinkedPR = _linkedPr,
        LinkedPRLastUpdate = _linkedPr is not null ? _lastUpdate : null,
        LinkedIssues = _linkedIssues,
        LinkedIssuesLastUpdate = _lastUpdate,
        ParentIssues = _parentIssues,
        ParentIssuesLastUpdate = _lastUpdate,
        Priority = _priority,
        PriorityLastUpdate = _priority is not null ? _lastUpdate : null,
        Group = _group,
        GroupLastUpdate = _group is not null ? _lastUpdate : null,
        AssignedTo = _assignedTo,
        AssignedToLastUpdate = _assignedTo is not null ? _lastUpdate : null,
        CreatedBy = _createdBy,
        CreatedByLastUpdate = _createdBy is not null ? _lastUpdate : null,
        LastUpdate = _lastUpdate,
        CreatedAt = _createdAt
    };
}
