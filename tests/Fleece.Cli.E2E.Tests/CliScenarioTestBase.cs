using System.Text;
using System.Text.Json;
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

        var issues = new List<Issue>();
        foreach (var path in Fs.Directory.GetFiles(dir, "issues_*.jsonl"))
        {
            foreach (var line in Fs.File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var issue = JsonSerializer.Deserialize(line, FleeceJsonContext.Default.Issue);
                if (issue is not null)
                {
                    issues.Add(issue);
                }
            }
        }
        return issues;
    }

    protected IReadOnlyList<Tombstone> LoadTombstones()
    {
        var dir = Path.Combine(BasePath, ".fleece");
        if (!Fs.Directory.Exists(dir))
        {
            return Array.Empty<Tombstone>();
        }

        var tombstones = new List<Tombstone>();
        foreach (var path in Fs.Directory.GetFiles(dir, "tombstones_*.jsonl"))
        {
            foreach (var line in Fs.File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var ts = JsonSerializer.Deserialize(line, FleeceJsonContext.Default.Tombstone);
                if (ts is not null)
                {
                    tombstones.Add(ts);
                }
            }
        }
        return tombstones;
    }

    protected Task AssertStdoutSnapshot()
        => Verifier.Verify(Console.Output);

    protected JsonElement ParseJsonOutput()
        => JsonDocument.Parse(Stdout).RootElement;
}
