using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Mutable counterpart of the immutable <see cref="Issue"/> record, used by the replay
/// engine to apply events incrementally without allocating a fresh record per event.
/// </summary>
internal sealed class IssueBuilder
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueStatus Status { get; set; }
    public IssueType Type { get; set; }
    public int? LinkedPR { get; set; }
    public List<string> LinkedIssues { get; } = [];
    public List<ParentIssueRef> ParentIssues { get; } = [];
    public int? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public List<string> Tags { get; } = [];
    public string? WorkingBranchId { get; set; }
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Series;
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdate { get; set; }

    public static IssueBuilder FromIssue(Issue issue)
    {
        var b = new IssueBuilder
        {
            Id = issue.Id,
            Title = issue.Title,
            Description = issue.Description,
            Status = issue.Status,
            Type = issue.Type,
            LinkedPR = issue.LinkedPR,
            Priority = issue.Priority,
            AssignedTo = issue.AssignedTo,
            WorkingBranchId = issue.WorkingBranchId,
            ExecutionMode = issue.ExecutionMode,
            CreatedBy = issue.CreatedBy,
            CreatedAt = issue.CreatedAt,
            LastUpdate = issue.LastUpdate,
        };
        b.LinkedIssues.AddRange(issue.LinkedIssues);
        b.ParentIssues.AddRange(issue.ParentIssues);
        b.Tags.AddRange(issue.Tags);
        return b;
    }

    public Issue ToIssue() => new()
    {
        Id = Id,
        Title = Title,
        Description = Description,
        Status = Status,
        Type = Type,
        LinkedPR = LinkedPR,
        LinkedIssues = LinkedIssues.ToList(),
        ParentIssues = ParentIssues.ToList(),
        Priority = Priority,
        AssignedTo = AssignedTo,
        Tags = Tags.ToList(),
        WorkingBranchId = WorkingBranchId,
        ExecutionMode = ExecutionMode,
        CreatedBy = CreatedBy,
        CreatedAt = CreatedAt,
        LastUpdate = LastUpdate,
    };

    public void ApplyCreate(CreateEvent evt)
    {
        Id = evt.IssueId;
        if (evt.Data.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in evt.Data.EnumerateObject())
            {
                ApplyDataProperty(prop.Name, prop.Value);
            }
        }
        if (CreatedBy is null && evt.By is not null)
        {
            CreatedBy = evt.By;
        }
        if (CreatedAt == default)
        {
            CreatedAt = evt.At;
        }
        if (LastUpdate == default || LastUpdate < evt.At)
        {
            LastUpdate = evt.At;
        }
    }

    public void ApplySet(SetEvent evt)
    {
        SetScalar(evt.Property, evt.Value);
        if (LastUpdate < evt.At)
        {
            LastUpdate = evt.At;
        }
    }

    public void ApplyAdd(AddEvent evt)
    {
        AddToArray(evt.Property, evt.Value);
        if (LastUpdate < evt.At)
        {
            LastUpdate = evt.At;
        }
    }

    public void ApplyRemove(RemoveEvent evt)
    {
        RemoveFromArray(evt.Property, evt.Value);
        if (LastUpdate < evt.At)
        {
            LastUpdate = evt.At;
        }
    }

    /// <summary>
    /// Applies a property from the <c>create.data</c> payload. Tolerant of unknown keys
    /// (forward-compat) and arrays for collection properties.
    /// </summary>
    private void ApplyDataProperty(string name, JsonElement value)
    {
        switch (name)
        {
            case "id":
                if (value.ValueKind == JsonValueKind.String)
                {
                    Id = value.GetString() ?? Id;
                }
                break;
            case "linkedIssues":
                LinkedIssues.Clear();
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in value.EnumerateArray())
                    {
                        var s = elem.GetString();
                        if (s is not null)
                        {
                            LinkedIssues.Add(s);
                        }
                    }
                }
                break;
            case "parentIssues":
                ParentIssues.Clear();
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in value.EnumerateArray())
                    {
                        var pref = JsonSerializer.Deserialize(elem, EventSourcingJsonContext.Default.ParentIssueRef);
                        if (pref is not null)
                        {
                            ParentIssues.Add(pref);
                        }
                    }
                }
                break;
            case "tags":
                Tags.Clear();
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in value.EnumerateArray())
                    {
                        var s = elem.GetString();
                        if (s is not null)
                        {
                            Tags.Add(s);
                        }
                    }
                }
                break;
            default:
                SetScalar(name, value, throwOnUnknown: false);
                break;
        }
    }

    private void SetScalar(string name, JsonElement value, bool throwOnUnknown = true)
    {
        switch (name)
        {
            case "title":
                Title = value.GetString() ?? string.Empty;
                break;
            case "description":
                Description = NullableString(value);
                break;
            case "status":
                Status = ParseEnum<IssueStatus>(value, Status);
                break;
            case "type":
                Type = ParseEnum<IssueType>(value, Type);
                break;
            case "linkedPR":
                LinkedPR = NullableInt(value);
                break;
            case "priority":
                Priority = NullableInt(value);
                break;
            case "assignedTo":
                AssignedTo = NullableString(value);
                break;
            case "workingBranchId":
                WorkingBranchId = NullableString(value);
                break;
            case "executionMode":
                ExecutionMode = ParseEnum<ExecutionMode>(value, ExecutionMode);
                break;
            case "createdBy":
                CreatedBy = NullableString(value);
                break;
            case "createdAt":
                if (value.ValueKind != JsonValueKind.Null)
                {
                    CreatedAt = value.GetDateTimeOffset();
                }
                break;
            case "lastUpdate":
                if (value.ValueKind != JsonValueKind.Null)
                {
                    LastUpdate = value.GetDateTimeOffset();
                }
                break;
            default:
                if (throwOnUnknown)
                {
                    throw new ArgumentException($"Unknown scalar property '{name}' for set event.");
                }
                break;
        }
    }

    private void AddToArray(string name, JsonElement value)
    {
        switch (name)
        {
            case "linkedIssues":
                {
                    var s = value.GetString();
                    if (s is null)
                    {
                        return;
                    }
                    if (!LinkedIssues.Contains(s))
                    {
                        LinkedIssues.Add(s);
                    }
                    break;
                }
            case "tags":
                {
                    var s = value.GetString();
                    if (s is null)
                    {
                        return;
                    }
                    if (!Tags.Contains(s))
                    {
                        Tags.Add(s);
                    }
                    break;
                }
            case "parentIssues":
                {
                    var pref = JsonSerializer.Deserialize(value, EventSourcingJsonContext.Default.ParentIssueRef);
                    if (pref is null)
                    {
                        return;
                    }
                    // Upsert by parentIssue key — re-adding the same key replaces the prior entry.
                    ParentIssues.RemoveAll(p => string.Equals(p.ParentIssue, pref.ParentIssue, StringComparison.Ordinal));
                    ParentIssues.Add(pref);
                    break;
                }
            default:
                throw new ArgumentException($"Unknown array property '{name}' for add event.");
        }
    }

    private void RemoveFromArray(string name, JsonElement value)
    {
        switch (name)
        {
            case "linkedIssues":
                {
                    var s = value.GetString();
                    if (s is not null)
                    {
                        LinkedIssues.Remove(s);
                    }
                    break;
                }
            case "tags":
                {
                    var s = value.GetString();
                    if (s is not null)
                    {
                        Tags.Remove(s);
                    }
                    break;
                }
            case "parentIssues":
                {
                    string? key = null;
                    if (value.ValueKind == JsonValueKind.Object &&
                        value.TryGetProperty("parentIssue", out var idElem) &&
                        idElem.ValueKind == JsonValueKind.String)
                    {
                        key = idElem.GetString();
                    }
                    else if (value.ValueKind == JsonValueKind.String)
                    {
                        key = value.GetString();
                    }
                    if (key is not null)
                    {
                        ParentIssues.RemoveAll(p => string.Equals(p.ParentIssue, key, StringComparison.Ordinal));
                    }
                    break;
                }
            default:
                throw new ArgumentException($"Unknown array property '{name}' for remove event.");
        }
    }

    private static string? NullableString(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetString();

    private static int? NullableInt(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();

    private static T ParseEnum<T>(JsonElement value, T fallback) where T : struct, Enum
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return fallback;
        }
        var s = value.GetString();
        return s is not null && Enum.TryParse<T>(s, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
