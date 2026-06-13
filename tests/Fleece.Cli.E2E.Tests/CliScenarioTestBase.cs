using System.Text;
using System.Text.Json;
using Fleece.Cli.E2E.Tests.Fakes;
using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Hermetic in-memory GitHub backend used by <see cref="RunWithGitHubAsync"/>. Lets the
    /// promote/absorb/auth surfaces run without network access.
    /// </summary>
    protected FakeGitHubService GitHub { get; private set; } = null!;

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
        GitHub = new FakeGitHubService();
        _stdoutBuffer.Clear();
    }

    protected Task<int> RunAsync(params string[] args)
        => CliApp.RunAsync(args, BasePath, Fs, Console);

    /// <summary>
    /// Runs the CLI with the hermetic <see cref="GitHub"/> backend substituted for the real
    /// OctoKit-backed <c>IGitHubService</c>. The registration added here wins because DI resolves
    /// the last registration.
    /// </summary>
    protected Task<int> RunWithGitHubAsync(params string[] args)
        => CliApp.RunAsync(args, BasePath, Fs, Console,
            services => services.AddSingleton<IGitHubService>(GitHub));

    protected IReadOnlyList<Issue> LoadIssues()
    {
        var dir = Path.Combine(BasePath, ".fleece");
        if (!Fs.Directory.Exists(dir))
        {
            return Array.Empty<Issue>();
        }

        // Replay every per-issue log using the same engine the CLI uses.
        var eventStore = new EventStore(BasePath, Fs);
        var replay = new ReplayEngine(eventStore);
        var logs = eventStore.GetAllIssueLogPathsAsync().GetAwaiter().GetResult();
        var state = replay.ReplayAsync(logs).GetAwaiter().GetResult();
        // Sort by CreatedAt so snapshot tests see issues in creation order.
        return state.Values
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
    }

    protected IReadOnlyList<Tombstone> LoadTombstones()
    {
        // Tombstones are not maintained under the v4 per-issue log model.
        return Array.Empty<Tombstone>();
    }

    protected Task AssertStdoutSnapshot()
        => Verifier.Verify(Console.Output);

    protected JsonElement ParseJsonOutput()
        => JsonDocument.Parse(Stdout).RootElement;
}
