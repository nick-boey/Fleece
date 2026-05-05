using Fleece.Cli.Commands;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
[Category("install")]
public class InstallScenarios : CliScenarioTestBase
{
    private const string GitConfigGitHubRemote =
        "[remote \"origin\"]\n\turl = https://github.com/example/example.git\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n";

    [Test]
    public async Task Install_creates_pre_commit_hook_with_fleece_block()
    {
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".git"));

        var exit = await RunAsync("install");
        exit.Should().Be(0);

        var hookPath = Path.Combine(BasePath, ".git", "hooks", "pre-commit");
        Fs.File.Exists(hookPath).Should().BeTrue();
        var content = await Fs.File.ReadAllTextAsync(hookPath);
        content.Should().Contain(InstallCommand.FleeceHookBlockStart);
        content.Should().Contain(InstallCommand.FleeceHookBlockEnd);
        content.Should().Contain("git add .fleece/changes/");
    }

    [Test]
    public async Task Install_skips_pre_commit_when_not_in_a_git_repo()
    {
        var exit = await RunAsync("install");
        exit.Should().Be(0);

        Fs.Directory.Exists(Path.Combine(BasePath, ".git", "hooks")).Should().BeFalse();
    }

    [Test]
    public async Task Install_is_idempotent_for_pre_commit_hook()
    {
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".git"));

        await RunAsync("install");
        var hookPath = Path.Combine(BasePath, ".git", "hooks", "pre-commit");
        var first = await Fs.File.ReadAllTextAsync(hookPath);

        await RunAsync("install");
        var second = await Fs.File.ReadAllTextAsync(hookPath);

        second.Should().Be(first);
        second.Split(InstallCommand.FleeceHookBlockStart).Length.Should().Be(2,
            because: "the fleece block must appear exactly once");
    }

    [Test]
    public async Task Install_preserves_existing_pre_commit_hook_content()
    {
        var hooksDir = Path.Combine(BasePath, ".git", "hooks");
        Fs.Directory.CreateDirectory(hooksDir);
        var hookPath = Path.Combine(hooksDir, "pre-commit");
        await Fs.File.WriteAllTextAsync(hookPath, "#!/bin/sh\necho 'user hook'\n");

        await RunAsync("install");

        var content = await Fs.File.ReadAllTextAsync(hookPath);
        content.Should().Contain("echo 'user hook'");
        content.Should().Contain(InstallCommand.FleeceHookBlockStart);
    }

    [Test]
    public async Task Install_adds_gitignore_entries_for_active_change_and_replay_cache()
    {
        var exit = await RunAsync("install");
        exit.Should().Be(0);

        var gitignorePath = Path.Combine(BasePath, ".gitignore");
        Fs.File.Exists(gitignorePath).Should().BeTrue();
        var content = await Fs.File.ReadAllTextAsync(gitignorePath);
        content.Should().Contain(".fleece/.active-change");
        content.Should().Contain(".fleece/.replay-cache");
    }

    [Test]
    public async Task Install_does_not_duplicate_existing_gitignore_entries()
    {
        var gitignorePath = Path.Combine(BasePath, ".gitignore");
        await Fs.File.WriteAllTextAsync(gitignorePath, "bin/\n.fleece/.active-change\n.fleece/.replay-cache\n");

        await RunAsync("install");

        var content = await Fs.File.ReadAllTextAsync(gitignorePath);
        content.Split('\n').Count(l => l.Trim() == ".fleece/.active-change").Should().Be(1);
        content.Split('\n').Count(l => l.Trim() == ".fleece/.replay-cache").Should().Be(1);
    }

    [Test]
    public async Task Install_writes_github_action_when_remote_is_github_and_workflows_dir_exists()
    {
        var gitDir = Path.Combine(BasePath, ".git");
        Fs.Directory.CreateDirectory(gitDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(gitDir, "config"), GitConfigGitHubRemote);
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".github", "workflows"));

        await RunAsync("install");

        var workflowPath = Path.Combine(BasePath, ".github", "workflows", InstallCommand.GitHubWorkflowFileName);
        Fs.File.Exists(workflowPath).Should().BeTrue();
        var content = await Fs.File.ReadAllTextAsync(workflowPath);
        content.Should().Contain("schedule:");
        content.Should().Contain("workflow_dispatch:");
        content.Should().Contain("fleece project");
    }

    [Test]
    public async Task Install_does_not_overwrite_existing_workflow_file()
    {
        var gitDir = Path.Combine(BasePath, ".git");
        Fs.Directory.CreateDirectory(gitDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(gitDir, "config"), GitConfigGitHubRemote);
        var workflowsDir = Path.Combine(BasePath, ".github", "workflows");
        Fs.Directory.CreateDirectory(workflowsDir);
        var workflowPath = Path.Combine(workflowsDir, InstallCommand.GitHubWorkflowFileName);
        await Fs.File.WriteAllTextAsync(workflowPath, "existing: content\n");

        await RunAsync("install");

        var content = await Fs.File.ReadAllTextAsync(workflowPath);
        content.Should().Be("existing: content\n");
    }

    [Test]
    public async Task Install_skips_workflow_on_non_github_repository()
    {
        var gitDir = Path.Combine(BasePath, ".git");
        Fs.Directory.CreateDirectory(gitDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(gitDir, "config"),
            "[remote \"origin\"]\n\turl = https://gitlab.example/foo/bar.git\n");
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".github", "workflows"));

        await RunAsync("install");

        var workflowPath = Path.Combine(BasePath, ".github", "workflows", InstallCommand.GitHubWorkflowFileName);
        Fs.File.Exists(workflowPath).Should().BeFalse();
    }
}
