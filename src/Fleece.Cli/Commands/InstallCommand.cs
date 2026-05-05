using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fleece.Cli.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class InstallCommand : AsyncCommand<InstallSettings>
{
    internal const string FleeceHookBlockStart = "# >>> fleece block >>>";
    internal const string FleeceHookBlockEnd = "# <<< fleece block <<<";
    internal const string GitHubWorkflowFileName = "fleece-project.yml";

    private const string ClaudeDirectory = ".claude";
    private const string SettingsFileName = "settings.json";

    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;

    public InstallCommand(IAnsiConsole console, IFileSystem fileSystem, BasePathProvider basePath)
    {
        _console = console;
        _fileSystem = fileSystem;
        _basePath = basePath.BasePath;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, InstallSettings settings)
    {
        await InstallClaudeHooksAsync();
        await InstallPreCommitHookAsync();
        await EnsureGitignoreEntriesAsync();
        await MaybeInstallGitHubWorkflowAsync();
        return 0;
    }

    private async Task InstallClaudeHooksAsync()
    {
        var claudeDir = _fileSystem.Path.Combine(_basePath, ClaudeDirectory);
        var settingsPath = _fileSystem.Path.Combine(claudeDir, SettingsFileName);

        _fileSystem.Directory.CreateDirectory(claudeDir);

        JsonObject root;
        if (_fileSystem.File.Exists(settingsPath))
        {
            var existing = await _fileSystem.File.ReadAllTextAsync(settingsPath);
            root = JsonNode.Parse(existing)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var hooks = root["hooks"]?.AsObject() ?? new JsonObject();
        var sessionStartHooks = hooks["SessionStart"]?.AsArray() ?? new JsonArray();

        var hasPrimeHook = false;
        foreach (var hook in sessionStartHooks)
        {
            var inner = hook?["hooks"]?.AsArray();
            if (inner is null)
            {
                continue;
            }
            foreach (var ih in inner)
            {
                if (ih?["command"]?.ToString() == "fleece prime")
                {
                    hasPrimeHook = true;
                    break;
                }
            }
            if (hasPrimeHook)
            {
                break;
            }
        }

        if (!hasPrimeHook)
        {
            sessionStartHooks.Add(new JsonObject
            {
                ["hooks"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "command",
                        ["command"] = "fleece prime",
                    },
                },
            });
        }

        hooks["SessionStart"] = sessionStartHooks;
        root["hooks"] = hooks;

        var options = new JsonSerializerOptions { WriteIndented = true };
        await _fileSystem.File.WriteAllTextAsync(settingsPath, root.ToJsonString(options));

        _console.MarkupLine("[green]Claude Code hooks installed.[/]");
        _console.MarkupLine($"[dim]  Settings: {settingsPath}[/]");
    }

    private async Task InstallPreCommitHookAsync()
    {
        var hooksDir = _fileSystem.Path.Combine(_basePath, ".git", "hooks");
        if (!_fileSystem.Directory.Exists(_fileSystem.Path.Combine(_basePath, ".git")))
        {
            _console.MarkupLine("[yellow]Skipping pre-commit hook: not in a git repository.[/]");
            return;
        }
        _fileSystem.Directory.CreateDirectory(hooksDir);

        var hookPath = _fileSystem.Path.Combine(hooksDir, "pre-commit");
        var fleeceBlock = BuildFleeceHookBlock();

        string newContent;
        if (_fileSystem.File.Exists(hookPath))
        {
            var existing = await _fileSystem.File.ReadAllTextAsync(hookPath);
            newContent = ReplaceOrAppendBlock(existing, fleeceBlock);
        }
        else
        {
            newContent = "#!/bin/sh\n" + fleeceBlock;
        }

        await _fileSystem.File.WriteAllTextAsync(hookPath, newContent);
        TryMarkExecutable(hookPath);
        _console.MarkupLine($"[green]pre-commit hook installed:[/] {hookPath}");
    }

    private static string BuildFleeceHookBlock()
    {
        // Stage event-sourcing change files on every commit. Snapshot/tombstones are
        // staged opportunistically on the default branch (the projection writes there).
        return string.Join('\n', new[]
        {
            FleeceHookBlockStart,
            "if [ -d .fleece/changes ]; then git add .fleece/changes/; fi",
            "current_branch=$(git symbolic-ref --short HEAD 2>/dev/null || echo \"\")",
            "default_branch=$(git config --get fleece.defaultBranch 2>/dev/null || echo \"main\")",
            "if [ \"$current_branch\" = \"$default_branch\" ]; then",
            "  if [ -f .fleece/issues.jsonl ]; then git add .fleece/issues.jsonl; fi",
            "  if [ -f .fleece/tombstones.jsonl ]; then git add .fleece/tombstones.jsonl; fi",
            "fi",
            FleeceHookBlockEnd,
            "",
        });
    }

    internal static string ReplaceOrAppendBlock(string existing, string block)
    {
        var startIdx = existing.IndexOf(FleeceHookBlockStart, StringComparison.Ordinal);
        var endIdx = existing.IndexOf(FleeceHookBlockEnd, StringComparison.Ordinal);
        if (startIdx >= 0 && endIdx > startIdx)
        {
            var afterEnd = endIdx + FleeceHookBlockEnd.Length;
            // Consume one trailing newline to avoid accumulating blank lines on each install.
            if (afterEnd < existing.Length && existing[afterEnd] == '\n')
            {
                afterEnd++;
            }
            return existing[..startIdx] + block + existing[afterEnd..];
        }
        var trimmed = existing.TrimEnd('\n');
        return trimmed + "\n\n" + block;
    }

    private void TryMarkExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            // System.IO.Abstractions does not expose chmod; only the real filesystem can be
            // marked executable. MockFileSystem simply ignores this call.
            if (_fileSystem is Testably.Abstractions.RealFileSystem)
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        catch
        {
            // Best-effort; failures here are non-fatal.
        }
    }

    private async Task EnsureGitignoreEntriesAsync()
    {
        var gitignorePath = _fileSystem.Path.Combine(_basePath, ".gitignore");
        var entries = new[]
        {
            ".fleece/.active-change",
            ".fleece/.replay-cache",
        };

        var existing = _fileSystem.File.Exists(gitignorePath)
            ? await _fileSystem.File.ReadAllTextAsync(gitignorePath)
            : string.Empty;

        var lines = existing.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var added = new List<string>();
        foreach (var entry in entries)
        {
            if (!lines.Any(l => string.Equals(l.Trim(), entry, StringComparison.Ordinal)))
            {
                added.Add(entry);
            }
        }
        if (added.Count == 0)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(existing.TrimEnd('\n'));
        if (sb.Length > 0)
        {
            sb.Append('\n');
        }
        foreach (var entry in added)
        {
            sb.Append(entry);
            sb.Append('\n');
        }
        await _fileSystem.File.WriteAllTextAsync(gitignorePath, sb.ToString());
        _console.MarkupLine($"[green].gitignore updated with {added.Count} entry(ies).[/]");
    }

    private async Task MaybeInstallGitHubWorkflowAsync()
    {
        var gitConfigPath = _fileSystem.Path.Combine(_basePath, ".git", "config");
        if (!_fileSystem.File.Exists(gitConfigPath))
        {
            return;
        }
        var gitConfig = await _fileSystem.File.ReadAllTextAsync(gitConfigPath);
        if (!HasGitHubRemote(gitConfig))
        {
            _console.MarkupLine("[dim]No github.com remote detected; skipping GitHub Action template.[/]");
            return;
        }

        var workflowsDir = _fileSystem.Path.Combine(_basePath, ".github", "workflows");
        if (!_fileSystem.Directory.Exists(workflowsDir))
        {
            _console.MarkupLine("[dim]No .github/workflows/ directory; skipping GitHub Action template.[/]");
            return;
        }

        var workflowPath = _fileSystem.Path.Combine(workflowsDir, GitHubWorkflowFileName);
        if (_fileSystem.File.Exists(workflowPath))
        {
            _console.MarkupLine($"[yellow]warning: {workflowPath} already exists; not overwriting. Reconcile manually.[/]");
            return;
        }

        await _fileSystem.File.WriteAllTextAsync(workflowPath, BuildWorkflowYaml());
        _console.MarkupLine($"[green]GitHub Action template installed:[/] {workflowPath}");
    }

    internal static bool HasGitHubRemote(string gitConfig)
    {
        if (string.IsNullOrWhiteSpace(gitConfig))
        {
            return false;
        }
        return gitConfig.Contains("github.com", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildWorkflowYaml()
    {
        return """
name: Fleece Daily Projection

on:
  schedule:
    - cron: "0 6 * * *"
  workflow_dispatch:

permissions:
  contents: write

jobs:
  project:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install Fleece
        run: dotnet tool install --global Fleece.Cli

      - name: Project events
        run: fleece project

      - name: Commit and push
        run: |
          git config user.name "fleece-bot"
          git config user.email "fleece-bot@users.noreply.github.com"
          if ! git diff --staged --quiet; then
            git commit -m "chore: project fleece events"
            git push
          else
            echo "Nothing to project."
          fi
""";
    }
}
