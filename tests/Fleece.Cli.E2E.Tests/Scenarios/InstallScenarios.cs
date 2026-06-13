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
        // v4 stages the whole .fleece/ directory; there are no merge markers to write.
        content.Should().Contain("git add .fleece");
        content.Should().Contain("fleece seal");
        content.Should().NotContain("fleece link --merge");
        content.Should().NotContain(".git/MERGE_HEAD");
    }

    [Test]
    public async Task Install_does_not_create_pre_merge_commit_hook()
    {
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".git"));

        var exit = await RunAsync("install");
        exit.Should().Be(0);

        // v4 removed the merge-marker mechanism, so no pre-merge-commit hook is installed.
        var hookPath = Path.Combine(BasePath, ".git", "hooks", "pre-merge-commit");
        Fs.File.Exists(hookPath).Should().BeFalse();
    }

    [Test]
    public async Task Install_strips_legacy_pre_merge_commit_fleece_block()
    {
        var hooksDir = Path.Combine(BasePath, ".git", "hooks");
        Fs.Directory.CreateDirectory(hooksDir);
        var hookPath = Path.Combine(hooksDir, "pre-merge-commit");
        // A v3 install left a fleece-managed block here; re-running install removes it.
        await Fs.File.WriteAllTextAsync(hookPath,
            $"#!/bin/sh\n{InstallCommand.FleeceHookBlockStart}\nfleece link --merge\n{InstallCommand.FleeceHookBlockEnd}\n");

        await RunAsync("install");

        // Only the shebang remained after stripping, so the hook file is removed entirely.
        Fs.File.Exists(hookPath).Should().BeFalse();
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
    public async Task Install_adds_no_gitignore_pointer_or_cache_entries()
    {
        var exit = await RunAsync("install");
        exit.Should().Be(0);

        // v4 per-issue logs keep no gitignored pointer/cache files, so install adds none.
        var gitignorePath = Path.Combine(BasePath, ".gitignore");
        if (Fs.File.Exists(gitignorePath))
        {
            var content = await Fs.File.ReadAllTextAsync(gitignorePath);
            content.Should().NotContain(".fleece/.active-change");
            content.Should().NotContain(".fleece/.replay-cache");
        }
    }

    [Test]
    public async Task Install_writes_ci_gate_workflow_when_remote_is_github_and_workflows_dir_exists()
    {
        var gitDir = Path.Combine(BasePath, ".git");
        Fs.Directory.CreateDirectory(gitDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(gitDir, "config"), GitConfigGitHubRemote);
        Fs.Directory.CreateDirectory(Path.Combine(BasePath, ".github", "workflows"));

        await RunAsync("install");

        var workflowPath = Path.Combine(BasePath, ".github", "workflows", InstallCommand.GitHubWorkflowFileName);
        Fs.File.Exists(workflowPath).Should().BeTrue();
        var content = await Fs.File.ReadAllTextAsync(workflowPath);
        content.Should().Contain("pull_request:");
        content.Should().Contain(".fleece/issues");
        content.Should().NotContain("fleece project");
        content.Should().NotContain("schedule:");
    }

    [Test]
    public async Task Install_removes_obsolete_projection_workflow()
    {
        var gitDir = Path.Combine(BasePath, ".git");
        Fs.Directory.CreateDirectory(gitDir);
        await Fs.File.WriteAllTextAsync(Path.Combine(gitDir, "config"), GitConfigGitHubRemote);
        var workflowsDir = Path.Combine(BasePath, ".github", "workflows");
        Fs.Directory.CreateDirectory(workflowsDir);
        var legacyPath = Path.Combine(workflowsDir, InstallCommand.LegacyGitHubWorkflowFileName);
        await Fs.File.WriteAllTextAsync(legacyPath, "jobs:\n  project:\n    steps:\n      - run: fleece project\n");

        await RunAsync("install");

        Fs.File.Exists(legacyPath).Should().BeFalse();
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

    [Test]
    public async Task Install_writes_claude_memory_block_with_philosophy_decision_rule_and_skill_pointer()
    {
        await RunAsync("install");

        var memoryPath = Path.Combine(BasePath, "CLAUDE.md");
        Fs.File.Exists(memoryPath).Should().BeTrue();
        var content = await Fs.File.ReadAllTextAsync(memoryPath);
        content.Should().Contain(InstallCommand.ClaudeMemoryBlockStart);
        content.Should().Contain("ephemeral");
        content.Should().Contain("fleece seal");
        // The blocks-this-PR → Fleece / durable → GitHub decision rule.
        content.Should().Contain("GitHub issue");
        // Pointer to the installed skill so a pull-based agent knows it exists.
        content.Should().Contain(".claude/skills/fleece");
    }

    [Test]
    public async Task Install_writes_fleece_skill_with_description_and_managed_header()
    {
        await RunAsync("install");

        var skillPath = Path.Combine(BasePath, ".claude", "skills", "fleece", "SKILL.md");
        Fs.File.Exists(skillPath).Should().BeTrue();
        var content = await Fs.File.ReadAllTextAsync(skillPath);
        content.Should().Contain("name: fleece");
        content.Should().Contain("description:");
        content.Should().Contain("managed by `fleece install`");
    }

    [Test]
    public async Task Install_writes_all_nine_skill_reference_topics()
    {
        await RunAsync("install");

        var referencesDir = Path.Combine(BasePath, ".claude", "skills", "fleece", "references");
        foreach (var topic in new[]
                 {
                     "hierarchy", "commands", "statuses", "sync", "json",
                     "next", "tree", "github", "v4-migration"
                 })
        {
            var path = Path.Combine(referencesDir, topic + ".md");
            Fs.File.Exists(path).Should().BeTrue(because: $"the '{topic}' reference should be installed");
        }
    }

    [Test]
    public async Task Install_overwrites_stale_skill_content_wholesale()
    {
        var skillDir = Path.Combine(BasePath, ".claude", "skills", "fleece");
        Fs.Directory.CreateDirectory(skillDir);
        var skillPath = Path.Combine(skillDir, "SKILL.md");
        await Fs.File.WriteAllTextAsync(skillPath, "STALE CONTENT FROM AN OLD VERSION");

        await RunAsync("install");

        var content = await Fs.File.ReadAllTextAsync(skillPath);
        content.Should().NotContain("STALE CONTENT");
        content.Should().Contain("name: fleece");
    }
}
