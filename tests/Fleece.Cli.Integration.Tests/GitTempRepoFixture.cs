using System.Diagnostics;

namespace Fleece.Cli.Integration.Tests;

public abstract class GitTempRepoFixture
{
    protected string TempDir { get; private set; } = null!;

    [SetUp]
    public void SetUp()
    {
        TempDir = Path.Combine(Path.GetTempPath(), "fleece-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);

        RunGit("init", "--initial-branch=main");
        RunGit("config", "user.name", "Fleece Tester");
        RunGit("config", "user.email", "tester@fleece.example");
        RunGit("config", "commit.gpgsign", "false");

        // Gitignore the volatile pointer files so they are never committed.
        // Without this, .active-change ends up tracked and conflicts on merge.
        File.WriteAllText(
            Path.Combine(TempDir, ".gitignore"),
            ".fleece/.active-change\n.fleece/.replay-cache\n");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(TempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; worst case the temp dir sticks around.
        }
    }

    protected void RunGit(params string[] args)
    {
        RunGitWithEnv(env: null, args);
    }

    /// <summary>
    /// Runs git with optional environment overrides. Use to set deterministic
    /// commit timestamps via GIT_AUTHOR_DATE / GIT_COMMITTER_DATE so tests that
    /// depend on commit order don't flake when commits land in the same second.
    /// </summary>
    protected void RunGitWithEnv(IReadOnlyDictionary<string, string>? env, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = TempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
        if (env is not null)
        {
            foreach (var (key, value) in env)
            {
                psi.Environment[key] = value;
            }
        }
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
        }
    }

    /// <summary>
    /// Commits with explicit author + committer timestamps. Use when test
    /// assertions depend on commit-order tiebreaks; same-second commits are
    /// otherwise indistinguishable to <c>git rev-list --date-order</c>.
    /// </summary>
    protected void RunGitCommit(string message, DateTimeOffset commitDate)
    {
        var iso = commitDate.ToString("yyyy-MM-ddTHH:mm:sszzz");
        RunGitWithEnv(
            new Dictionary<string, string>
            {
                ["GIT_AUTHOR_DATE"] = iso,
                ["GIT_COMMITTER_DATE"] = iso,
            },
            "commit", "-m", message);
    }

    protected string GitOutput(params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = TempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git");
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stdout;
    }

    protected Task<int> RunCliAsync(params string[] args)
        => CliApp.RunAsync(args, TempDir);
}
