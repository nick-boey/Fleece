using System.Text;
using System.Text.Json;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Spectre.Console.Testing;
using Testably.Abstractions.Testing;

namespace Fleece.Cli.E2E.Tests;

public abstract class CliScenarioTestBase
{
    protected const string BasePath = "/project";

    private static readonly object StdoutLock = new();
    private static StringBuilder _stdoutBuffer = null!;
    private static StringWriter _stdoutWriter = null!;
    private static TextWriter? _originalStdout;

    protected MockFileSystem Fs { get; private set; } = null!;
    protected TestConsole Console { get; private set; } = null!;

    protected string Stdout => _stdoutBuffer.ToString();

    [OneTimeSetUp]
    public void BaseOneTimeSetUp()
    {
        lock (StdoutLock)
        {
            if (_originalStdout is null)
            {
                _originalStdout = System.Console.Out;
                _stdoutBuffer = new StringBuilder();
                _stdoutWriter = new StringWriter(_stdoutBuffer);
                System.Console.SetOut(_stdoutWriter);
            }
        }
    }

    [SetUp]
    public void BaseSetUp()
    {
        Fs = new MockFileSystem();
        Fs.Directory.CreateDirectory(BasePath);
        Console = new TestConsole();
        _stdoutBuffer.Clear();
    }

    protected Task<int> RunAsync(params string[] args)
        => CliApp.RunAsync(args, BasePath, Fs, Console);

    protected IReadOnlyList<Issue> LoadIssues()
    {
        var dir = Path.Combine(BasePath, ".fleece");
        if (!Fs.Directory.Exists(dir))
        {
            return Array.Empty<Issue>();
        }

        // Replay snapshot + change files using the same engine the CLI uses.
        var snapshot = new SnapshotStore(BasePath, Fs);
        var eventStore = new EventStore(BasePath, Fs);
        var replay = new ReplayEngine(eventStore);
        var initial = snapshot.LoadSnapshotAsync().GetAwaiter().GetResult();
        var changeFiles = eventStore.GetAllChangeFilePathsAsync().GetAwaiter().GetResult();
        var state = changeFiles.Count == 0
            ? initial
            : replay.ReplayAsync(initial, changeFiles, NullEventGitContext.Instance).GetAwaiter().GetResult();
        // Sort by CreatedAt so snapshot tests see issues in creation order (matches the
        // legacy hashed-file append order that snapshot scrubbing was tuned for).
        return state.Values
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
    }

    protected IReadOnlyList<Tombstone> LoadTombstones()
    {
        var dir = Path.Combine(BasePath, ".fleece");
        if (!Fs.Directory.Exists(dir))
        {
            return Array.Empty<Tombstone>();
        }

        var snapshot = new SnapshotStore(BasePath, Fs);
        return snapshot.LoadTombstonesAsync().GetAwaiter().GetResult();
    }

    protected Task AssertStdoutSnapshot()
        => Verifier.Verify(Console.Output);

    protected JsonElement ParseJsonOutput()
        => JsonDocument.Parse(Stdout).RootElement;
}
