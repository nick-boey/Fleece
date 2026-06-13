using System.Buffers;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.Models;

namespace Fleece.Core.EventSourcing.Services.Legacy;

/// <summary>
/// Read-only revival of the pre-v4 "durable snapshot" layout: a <c>.fleece/issues.jsonl</c>
/// snapshot layered with per-session change files under <c>.fleece/changes/</c>
/// (<c>change_{guid}.jsonl</c>, ordered by a <c>follows</c> DAG). Used solely by the one-time
/// <c>fleece migrate</c> conversion to compute each issue's current state before re-emitting it
/// as a v4 per-issue log.
/// <para>
/// This revives only the READ/replay side that the v4 rewrite removed (the deleted
/// <c>SnapshotStore</c> read path plus the change-file layering trimmed out of
/// <c>ReplayEngine</c>). It never regenerates a snapshot, writes <c>.active-change</c>/
/// <c>.replay-cache</c>, or restores <c>fleece project</c>/<c>merge</c>.
/// </para>
/// </summary>
public sealed class DurableSnapshotReader
{
    internal const string FleeceDirectory = ".fleece";
    internal const string SnapshotFileName = "issues.jsonl";
    internal const string ChangesDirectory = "changes";
    internal const string ChangeFilePattern = "change_*.jsonl";

    private const string ChangeFilePrefix = "change_";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;

    public DurableSnapshotReader(string basePath, IFileSystem? fileSystem = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem ?? new Testably.Abstractions.RealFileSystem();
    }

    private string SnapshotPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory, SnapshotFileName);
    private string ChangesDirectoryPath => _fileSystem.Path.Combine(_basePath, FleeceDirectory, ChangesDirectory);

    /// <summary>True when a legacy durable snapshot (<c>.fleece/issues.jsonl</c>) is present.</summary>
    public bool IsDurableLayoutPresent() => _fileSystem.File.Exists(SnapshotPath);

    /// <summary>
    /// Replays the snapshot together with every change file (in <c>follows</c> order) and returns
    /// each issue's fully-applied current state. Returns an empty list when no snapshot is present.
    /// </summary>
    public async Task<IReadOnlyList<Issue>> ReadCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var builders = new Dictionary<string, IssueBuilder>(StringComparer.Ordinal);

        if (_fileSystem.File.Exists(SnapshotPath))
        {
            var content = await _fileSystem.File.ReadAllTextAsync(SnapshotPath, cancellationToken);
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                var issue = JsonSerializer.Deserialize(NormalizeEnums(line), EventSourcingJsonContext.Default.Issue);
                if (issue is not null)
                {
                    builders[issue.Id] = IssueBuilder.FromIssue(issue);
                }
            }
        }

        var changeFiles = _fileSystem.Directory.Exists(ChangesDirectoryPath)
            ? _fileSystem.Directory.GetFiles(ChangesDirectoryPath, ChangeFilePattern)
            : Array.Empty<string>();

        if (changeFiles.Length > 0)
        {
            var ordered = await SortByFollowsAsync(changeFiles, cancellationToken);
            foreach (var path in ordered)
            {
                await ApplyChangeFileAsync(builders, path, cancellationToken);
            }
        }

        return builders.Values.Select(b => b.ToIssue()).ToList();
    }

    /// <summary>Deletes the consumed snapshot and the entire <c>.fleece/changes/</c> directory.</summary>
    public void DeleteSources()
    {
        if (_fileSystem.File.Exists(SnapshotPath))
        {
            _fileSystem.File.Delete(SnapshotPath);
        }
        if (_fileSystem.Directory.Exists(ChangesDirectoryPath))
        {
            _fileSystem.Directory.Delete(ChangesDirectoryPath, recursive: true);
        }
    }

    private async Task ApplyChangeFileAsync(
        Dictionary<string, IssueBuilder> builders, string path, CancellationToken cancellationToken)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
        var lineNumber = 0;
        foreach (var rawLine in content.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            switch (TryReadString(line, "kind"))
            {
                case "meta":
                    // Positioning-only; consumed during follows ordering, never applied.
                    break;
                case "hard-delete":
                    {
                        var id = TryReadString(line, "issueId");
                        if (id is not null)
                        {
                            builders.Remove(id);
                        }
                        break;
                    }
                default:
                    {
                        var evt = EventJsonSerializer.ParseLine(NormalizeEnums(line), path, lineNumber);
                        ApplyEvent(builders, evt);
                        break;
                    }
            }
        }
    }

    private static void ApplyEvent(Dictionary<string, IssueBuilder> builders, IssueEvent evt)
    {
        switch (evt)
        {
            case CreateEvent c:
                if (!builders.TryGetValue(c.IssueId, out var cb))
                {
                    cb = new IssueBuilder();
                    builders[c.IssueId] = cb;
                }
                cb.ApplyCreate(c);
                break;
            case SetEvent s:
                if (builders.TryGetValue(s.IssueId, out var sb))
                {
                    sb.ApplySet(s);
                }
                break;
            case AddEvent a:
                if (builders.TryGetValue(a.IssueId, out var ab))
                {
                    ab.ApplyAdd(a);
                }
                break;
            case RemoveEvent r:
                if (builders.TryGetValue(r.IssueId, out var rb))
                {
                    rb.ApplyRemove(r);
                }
                break;
        }
    }

    // ----- removed-enum remapping (mirrors IssueStatusConverter / IssueTypeConverter) -----
    //
    // A genuine pre-v4 (v3) durable snapshot or change file can legitimately carry enum members
    // that v4 removed: type `Idea`, and status `Draft`/`Archived`/`Deleted`/`Spec`/`Next`/`Idea`.
    // The v4 event/snapshot JSON contexts use a plain string-enum converter that throws on those,
    // so we normalize the raw enum strings to their surviving v4 equivalents before parsing —
    // the same projection the hashed-file (Layout A) path performs via the legacy converters.

    private static readonly Dictionary<string, string> StatusRemap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idea"] = "Open",
        ["spec"] = "Open",
        ["next"] = "Open",
        ["draft"] = "Open",
        ["archived"] = "Promoted",
        ["deleted"] = "Closed",
    };

    private static readonly Dictionary<string, string> TypeRemap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idea"] = "Task",
    };

    /// <summary>
    /// Rewrites removed enum strings to their v4 equivalents. Handles issue-shaped objects
    /// (top-level <c>status</c>/<c>type</c>, plus a nested <c>data</c> object for create events)
    /// and <c>set</c> events (whose <c>value</c> is remapped when <c>property</c> is status/type).
    /// Returns the original line unchanged if it is not a JSON object.
    /// </summary>
    private static string NormalizeEnums(string line)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return line;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return line;
            }

            string? setProperty = null;
            if (doc.RootElement.TryGetProperty("kind", out var kind) &&
                kind.ValueKind == JsonValueKind.String && kind.GetString() == "set" &&
                doc.RootElement.TryGetProperty("property", out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                setProperty = prop.GetString();
            }

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteRemappedObject(writer, doc.RootElement, setProperty);
            }
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
    }

    private static void WriteRemappedObject(Utf8JsonWriter writer, JsonElement obj, string? setProperty)
    {
        writer.WriteStartObject();
        foreach (var member in obj.EnumerateObject())
        {
            var name = member.Name;
            var value = member.Value;

            if (value.ValueKind == JsonValueKind.String &&
                name == "status")
            {
                writer.WriteString(name, Remap(StatusRemap, value.GetString()));
            }
            else if (value.ValueKind == JsonValueKind.String &&
                name == "type")
            {
                writer.WriteString(name, Remap(TypeRemap, value.GetString()));
            }
            else if (value.ValueKind == JsonValueKind.String &&
                name == "value" && setProperty is "status" or "type")
            {
                var map = setProperty == "status" ? StatusRemap : TypeRemap;
                writer.WriteString(name, Remap(map, value.GetString()));
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName(name);
                // Nested objects (e.g. a create event's `data`) carry their own status/type keys.
                WriteRemappedObject(writer, value, setProperty: null);
            }
            else
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    private static string? Remap(Dictionary<string, string> map, string? raw) =>
        raw is not null && map.TryGetValue(raw, out var mapped) ? mapped : raw;

    private static string? TryReadString(string line, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(property, out var elem) &&
                elem.ValueKind == JsonValueKind.String)
            {
                return elem.GetString();
            }
        }
        catch (JsonException)
        {
            // Tolerant: a malformed line is handled by the regular parse path below.
        }
        return null;
    }

    // ----- follows-DAG ordering (trimmed from the pre-v4 ReplayEngine) -----

    /// <summary>
    /// Orders change files by their <c>follows</c> DAG: a file is emitted only after every
    /// predecessor it follows. Dangling follows (parents not present locally) are dropped;
    /// a multi-parent marker linearises its parents in listed order; ties break on GUID
    /// (ordinal). There is no git commit-ordinal input — this is a one-shot migration read.
    /// </summary>
    private async Task<IReadOnlyList<string>> SortByFollowsAsync(
        IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var nodes = new List<FileNode>(paths.Count);
        foreach (var path in paths)
        {
            var follows = await ReadFollowsAsync(path, cancellationToken);
            nodes.Add(new FileNode(ExtractGuid(path), path, follows));
        }

        var byGuid = nodes.ToDictionary(n => n.Guid, StringComparer.Ordinal);

        // Drop dangling follows (referenced GUIDs that aren't present locally).
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n.Follows.Count == 0)
            {
                continue;
            }
            var live = n.Follows.Where(byGuid.ContainsKey).ToList();
            if (live.Count != n.Follows.Count)
            {
                nodes[i] = n with { Follows = live };
            }
        }
        byGuid = nodes.ToDictionary(n => n.Guid, StringComparer.Ordinal);

        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var n in nodes)
        {
            inDegree[n.Guid] = 0;
            children[n.Guid] = [];
        }
        foreach (var n in nodes)
        {
            foreach (var parent in n.Follows)
            {
                children[parent].Add(n.Guid);
                inDegree[n.Guid]++;
            }
        }

        // Multi-parent marker: the ORDER of the follows list is load-bearing. Add explicit
        // Pi → Pi+1 edges so a merge marker linearises its parents. Skip self-loops, dupes,
        // and any edge that would introduce a cycle.
        foreach (var n in nodes)
        {
            if (n.Follows.Count < 2)
            {
                continue;
            }
            for (var i = 0; i < n.Follows.Count - 1; i++)
            {
                var from = n.Follows[i];
                var to = n.Follows[i + 1];
                if (string.Equals(from, to, StringComparison.Ordinal) ||
                    children[from].Contains(to, StringComparer.Ordinal) ||
                    CreatesCycle(from, to, children))
                {
                    continue;
                }
                children[from].Add(to);
                inDegree[to]++;
            }
        }

        var ready = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (guid, deg) in inDegree)
        {
            if (deg == 0)
            {
                ready.Add(guid);
            }
        }

        var result = new List<FileNode>(nodes.Count);
        while (ready.Count > 0)
        {
            var first = ready.Min!;
            ready.Remove(first);
            result.Add(byGuid[first]);
            foreach (var child in children[first])
            {
                if (--inDegree[child] == 0)
                {
                    ready.Add(child);
                }
            }
        }

        if (result.Count != nodes.Count)
        {
            // Defensive: a cycle would leave nodes unemitted. Append by GUID order.
            var emitted = result.Select(n => n.Guid).ToHashSet(StringComparer.Ordinal);
            foreach (var n in nodes.OrderBy(node => node.Guid, StringComparer.Ordinal))
            {
                if (!emitted.Contains(n.Guid))
                {
                    result.Add(n);
                }
            }
        }

        return result.Select(n => n.Path).ToList();
    }

    private async Task<IReadOnlyList<string>> ReadFollowsAsync(string path, CancellationToken cancellationToken)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            // The meta event (first non-empty line) carries follows.
            return ParseFollows(line);
        }
        return [];
    }

    private static IReadOnlyList<string> ParseFollows(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("follows", out var follows))
            {
                return Array.Empty<string>();
            }
            return follows.ValueKind switch
            {
                JsonValueKind.String => new[] { follows.GetString()! },
                JsonValueKind.Array => follows.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToArray(),
                _ => Array.Empty<string>(),
            };
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool CreatesCycle(string from, string to, Dictionary<string, List<string>> children)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(to);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (string.Equals(current, from, StringComparison.Ordinal))
            {
                return true;
            }
            if (!visited.Add(current))
            {
                continue;
            }
            if (children.TryGetValue(current, out var next))
            {
                foreach (var child in next)
                {
                    stack.Push(child);
                }
            }
        }
        return false;
    }

    private static string ExtractGuid(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        return name.StartsWith(ChangeFilePrefix, StringComparison.Ordinal)
            ? name[ChangeFilePrefix.Length..]
            : name;
    }

    private sealed record FileNode(string Guid, string Path, IReadOnlyList<string> Follows);
}
