using System.IO.Abstractions;
using Fleece.Core.EventSourcing.Events;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.EventSourcing.Services;

/// <summary>
/// Default <see cref="ILinkService"/>. The marker contains zero body events; its
/// only payload is the multi-parent <c>follows</c> meta event that lets replay
/// linearise parallel chains.
/// </summary>
public sealed class LinkService : ILinkService
{
    private const string ChangeFilePrefix = "change_";
    private const string ChangeFileExtension = ".jsonl";

    private readonly string _basePath;
    private readonly IFileSystem _fileSystem;
    private readonly IEventStore _eventStore;
    private readonly IGitService _gitService;
    private readonly Func<string> _guidFactory;

    public LinkService(
        string basePath,
        IFileSystem fileSystem,
        IEventStore eventStore,
        IGitService gitService,
        Func<string>? guidFactory = null)
    {
        _basePath = basePath;
        _fileSystem = fileSystem;
        _eventStore = eventStore;
        _gitService = gitService;
        _guidFactory = guidFactory ?? (() => Guid.NewGuid().ToString("N"));
    }

    public async Task<LinkResult> CreateMergeMarkerAsync(CancellationToken cancellationToken = default)
    {
        var mergeHeadPath = _fileSystem.Path.Combine(_basePath, ".git", "MERGE_HEAD");
        if (!_fileSystem.File.Exists(mergeHeadPath))
        {
            return new LinkResult(false, null, null, Array.Empty<string>(),
                "No merge in progress (no .git/MERGE_HEAD); nothing to link.");
        }

        var parents = await ComputePerSideLeavesAsync(mergeHeadPath, cancellationToken);
        if (parents.Count == 0)
        {
            // Fall back to working-tree leaves if no git-tracked files exist on either side
            // (e.g. tests that drop a MERGE_HEAD file but never commit any change files).
            parents = await ComputeLeavesAsync(cancellationToken);
        }
        if (parents.Count == 0)
        {
            return new LinkResult(false, null, null, Array.Empty<string>(),
                "No change files present; nothing to link.");
        }

        var changesDir = _fileSystem.Path.Combine(_basePath, ".fleece", "changes");
        _fileSystem.Directory.CreateDirectory(changesDir);

        var guid = _guidFactory();
        var markerPath = _fileSystem.Path.Combine(changesDir, $"{ChangeFilePrefix}{guid}{ChangeFileExtension}");
        if (_fileSystem.File.Exists(markerPath))
        {
            throw new InvalidOperationException(
                $"Generated GUID '{guid}' collides with existing change file at {markerPath}.");
        }

        var meta = new MetaEvent { Follows = parents };
        await _fileSystem.File.WriteAllTextAsync(
            markerPath, EventJsonSerializer.Serialize(meta) + "\n", cancellationToken);

        // Stage the marker so it lands in the merge commit. Best-effort: if git fails
        // (e.g. running outside a hook in a stale state) we still leave the file on disk.
        _gitService.RunGitCommand($"add \"{markerPath}\"");

        return new LinkResult(true, guid, markerPath, parents,
            $"Wrote merge marker {ChangeFilePrefix}{guid}{ChangeFileExtension} linking {parents.Count} parent(s).");
    }

    /// <summary>
    /// Per the spec: compute the DAG leaf set restricted to each side of the merge
    /// (HEAD and every entry in MERGE_HEAD), then union+dedupe. Reading file
    /// contents from the post-merge working tree is safe because change files are
    /// immutable once committed — same content regardless of which side we ask.
    /// </summary>
    private async Task<IReadOnlyList<string>> ComputePerSideLeavesAsync(
        string mergeHeadPath, CancellationToken cancellationToken)
    {
        var mergeHeadContent = await _fileSystem.File.ReadAllTextAsync(mergeHeadPath, cancellationToken);
        var sideShas = new List<string> { "HEAD" };
        foreach (var line in mergeHeadContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                sideShas.Add(trimmed);
            }
        }

        // Cache all meta events from the working tree once; commit-immutability of
        // change files lets us read content from the working tree even for files
        // that only exist in HEAD vs MERGE_HEAD.
        var allPaths = await _eventStore.GetAllChangeFilePathsAsync(cancellationToken);
        if (allPaths.Count == 0)
        {
            return Array.Empty<string>();
        }
        var followsByGuid = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var path in allPaths)
        {
            followsByGuid[EventStore.ExtractGuidFromPath(path)] =
                (await _eventStore.ReadMetaAsync(path, cancellationToken)).Follows ?? Array.Empty<string>();
        }

        // Order matters: HEAD-side leaves first (our side), then each MERGE_HEAD entry
        // in order. The replay engine enforces parent-order as an ordering edge, so
        // listing "our leaf" first means "incoming changes apply on top of our state."
        var leaves = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sideSha in sideShas)
        {
            var guidsOnSide = ListChangeFileGuidsAt(sideSha);
            if (guidsOnSide.Count == 0)
            {
                continue;
            }
            var hasDescendant = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in guidsOnSide)
            {
                if (!followsByGuid.TryGetValue(guid, out var follows))
                {
                    continue;
                }
                foreach (var parent in follows)
                {
                    if (guidsOnSide.Contains(parent))
                    {
                        hasDescendant.Add(parent);
                    }
                }
            }
            // Within one side, order leaves alphabetically for determinism.
            var sideLeaves = guidsOnSide.Where(g => !hasDescendant.Contains(g))
                .OrderBy(g => g, StringComparer.Ordinal)
                .ToList();
            foreach (var guid in sideLeaves)
            {
                if (seen.Add(guid))
                {
                    leaves.Add(guid);
                }
            }
        }
        return leaves;
    }

    /// <summary>
    /// Returns the change-file GUIDs reachable in the tree rooted at <paramref name="treeish"/>
    /// (a commit SHA or "HEAD"). Returns an empty set if the git command fails or no
    /// matching files exist.
    /// </summary>
    private HashSet<string> ListChangeFileGuidsAt(string treeish)
    {
        var args = $"ls-tree -r --name-only {treeish} -- .fleece/changes/";
        var (exitCode, output, _) = _gitService.RunGitCommand(args);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(line.Trim());
            if (string.IsNullOrEmpty(name) || !name.StartsWith(ChangeFilePrefix, StringComparison.Ordinal))
            {
                continue;
            }
            set.Add(name[ChangeFilePrefix.Length..]);
        }
        return set;
    }

    /// <summary>
    /// Working-tree leaf computation: build the follows-DAG over every change file
    /// on disk and return the set of leaves. Used when the git ls-tree path returns
    /// nothing (e.g. unit-test scenarios that drop MERGE_HEAD by hand without committing).
    /// </summary>
    private async Task<IReadOnlyList<string>> ComputeLeavesAsync(CancellationToken cancellationToken)
    {
        var allPaths = await _eventStore.GetAllChangeFilePathsAsync(cancellationToken);
        if (allPaths.Count == 0)
        {
            return Array.Empty<string>();
        }

        var guids = allPaths.Select(EventStore.ExtractGuidFromPath).ToList();
        var guidSet = guids.ToHashSet(StringComparer.Ordinal);
        var hasDescendant = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in allPaths)
        {
            var meta = await _eventStore.ReadMetaAsync(path, cancellationToken);
            foreach (var parent in meta.Follows)
            {
                if (guidSet.Contains(parent))
                {
                    hasDescendant.Add(parent);
                }
            }
        }

        var leaves = guids.Where(g => !hasDescendant.Contains(g)).ToList();
        leaves.Sort(StringComparer.Ordinal);
        return leaves;
    }
}
