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
    internal const string GitHubWorkflowFileName = "fleece-ci-gate.yml";
    internal const string LegacyGitHubWorkflowFileName = "fleece-project.yml";

    internal const string ClaudeMemoryBlockStart = "<!-- >>> fleece memory >>> -->";
    internal const string ClaudeMemoryBlockEnd = "<!-- <<< fleece memory <<< -->";

    private const string ClaudeDirectory = ".claude";
    private const string SettingsFileName = "settings.json";
    private const string ClaudeMemoryFileName = "CLAUDE.md";

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
        await InstallClaudeMemoryBlockAsync();
        await InstallPreCommitHookAsync();
        await RemovePreMergeCommitHookAsync();
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

    /// <summary>
    /// Writes (or refreshes) a fleece-managed block in the project's CLAUDE.md that states the
    /// v4 ephemeral-memory philosophy: issues are branch-local working memory; before opening a
    /// PR you resolve, promote durable work to GitHub Issues, then seal. The block is delimited by
    /// marker comments so re-running install updates it in place and never clobbers user content.
    /// </summary>
    private async Task InstallClaudeMemoryBlockAsync()
    {
        var memoryPath = _fileSystem.Path.Combine(_basePath, ClaudeMemoryFileName);
        var block = BuildClaudeMemoryBlock();

        string newContent;
        if (_fileSystem.File.Exists(memoryPath))
        {
            var existing = await _fileSystem.File.ReadAllTextAsync(memoryPath);
            newContent = ReplaceOrAppendBlock(existing, block, ClaudeMemoryBlockStart, ClaudeMemoryBlockEnd);
        }
        else
        {
            newContent = block;
        }

        await _fileSystem.File.WriteAllTextAsync(memoryPath, newContent);
        _console.MarkupLine($"[green]Fleece memory block written:[/] {memoryPath}");
    }

    internal static string BuildClaudeMemoryBlock()
    {
        return string.Join('\n', new[]
        {
            ClaudeMemoryBlockStart,
            "## Fleece: ephemeral working memory",
            "",
            "Fleece issues are **branch-local working memory**, not a durable backlog. They exist to",
            "track the work in flight on the current branch and are expected to be cleared before the",
            "branch merges. Anything that must outlive the branch belongs in GitHub Issues.",
            "",
            "- **Plan/track on the branch**: create issues with `fleece create`, decompose with",
            "  `--parent-issues`, and pick the next item with `fleece next`.",
            "- **Resolve before a PR**: every issue must reach an inactive status",
            "  (`complete`, `closed`, or `promoted`) before the branch is sealed.",
            "- **Promote durable work**: anything worth keeping past this branch goes to GitHub Issues",
            "  via `fleece promote <id> [<id>...]`. The issue is marked `promoted` and tagged",
            "  `promoted=<#>`.",
            "- **Seal before merging**: run `fleece seal` to archive the inactive issues to",
            "  `.fleece/archive/` and clear `.fleece/issues/`. The CI gate fails any PR that still has",
            "  live logs under `.fleece/issues/`.",
            "- **Absorb when needed**: `fleece absorb #<github-#>` pulls a GitHub issue back into",
            "  branch-local working memory.",
            "",
            "In short: branch-local memory in, durable work out to GitHub, seal, then merge.",
            ClaudeMemoryBlockEnd,
            "",
        });
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
        // v4 ephemeral memory: stage the whole .fleece/ directory so branch-local issue logs ride
        // along with this commit. There are no merge markers to write (the follows-DAG is gone).
        // The active-issue reminder is purely informational — it never exits non-zero, so the
        // commit always proceeds.
        return string.Join('\n', new[]
        {
            FleeceHookBlockStart,
            "if [ -d .fleece ]; then git add .fleece; fi",
            "if [ -d .fleece/issues ]; then",
            "  active=$(ls .fleece/issues/*.jsonl 2>/dev/null | wc -l | tr -d ' ')",
            "  if [ \"$active\" != \"0\" ]; then",
            "    echo \"fleece: $active active issue log(s) under .fleece/issues/. Run 'fleece seal' before opening a PR.\" 1>&2",
            "  fi",
            "fi",
            FleeceHookBlockEnd,
            "",
        });
    }

    /// <summary>
    /// v4 removes the merge-marker mechanism, so the pre-merge-commit hook is no longer installed.
    /// Re-running install strips any fleece-managed block left over from a v3 install; if that
    /// leaves only the shebang behind, the hook file is removed entirely.
    /// </summary>
    private async Task RemovePreMergeCommitHookAsync()
    {
        var hookPath = _fileSystem.Path.Combine(_basePath, ".git", "hooks", "pre-merge-commit");
        if (!_fileSystem.File.Exists(hookPath))
        {
            return;
        }

        var existing = await _fileSystem.File.ReadAllTextAsync(hookPath);
        if (!existing.Contains(FleeceHookBlockStart, StringComparison.Ordinal))
        {
            return;
        }

        var stripped = RemoveBlock(existing, FleeceHookBlockStart, FleeceHookBlockEnd);
        if (string.IsNullOrWhiteSpace(stripped) ||
            string.Equals(stripped.TrimEnd('\n'), "#!/bin/sh", StringComparison.Ordinal))
        {
            _fileSystem.File.Delete(hookPath);
            _console.MarkupLine($"[green]removed obsolete pre-merge-commit hook:[/] {hookPath}");
            return;
        }

        await _fileSystem.File.WriteAllTextAsync(hookPath, stripped);
        _console.MarkupLine($"[green]removed obsolete fleece block from pre-merge-commit hook:[/] {hookPath}");
    }

    internal static string ReplaceOrAppendBlock(string existing, string block)
        => ReplaceOrAppendBlock(existing, block, FleeceHookBlockStart, FleeceHookBlockEnd);

    internal static string ReplaceOrAppendBlock(string existing, string block, string start, string end)
    {
        var startIdx = existing.IndexOf(start, StringComparison.Ordinal);
        var endIdx = existing.IndexOf(end, StringComparison.Ordinal);
        if (startIdx >= 0 && endIdx > startIdx)
        {
            var afterEnd = endIdx + end.Length;
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

    internal static string RemoveBlock(string existing, string start, string end)
    {
        var startIdx = existing.IndexOf(start, StringComparison.Ordinal);
        var endIdx = existing.IndexOf(end, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx <= startIdx)
        {
            return existing;
        }
        var afterEnd = endIdx + end.Length;
        if (afterEnd < existing.Length && existing[afterEnd] == '\n')
        {
            afterEnd++;
        }
        // Trim a blank separator line preceding the block, if any.
        var before = existing[..startIdx].TrimEnd('\n');
        var after = existing[afterEnd..];
        if (before.Length == 0)
        {
            return after.TrimStart('\n');
        }
        return after.Length == 0 ? before + "\n" : before + "\n" + after.TrimStart('\n');
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
        // v4 per-issue logs keep no gitignored pointer/cache files. The per-issue logs
        // under .fleece/issues/ and the archives under .fleece/archive/ are tracked, so
        // there are no entries to add.
        var entries = Array.Empty<string>();

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

        // Remove the obsolete v3 daily-projection workflow if it was previously installed by fleece.
        var legacyWorkflowPath = _fileSystem.Path.Combine(workflowsDir, LegacyGitHubWorkflowFileName);
        if (_fileSystem.File.Exists(legacyWorkflowPath))
        {
            var legacy = await _fileSystem.File.ReadAllTextAsync(legacyWorkflowPath);
            if (legacy.Contains("fleece project", StringComparison.Ordinal))
            {
                _fileSystem.File.Delete(legacyWorkflowPath);
                _console.MarkupLine($"[green]removed obsolete projection workflow:[/] {legacyWorkflowPath}");
            }
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
        // CI gate: fail any PR whose branch still carries live Fleece issue logs. The check is
        // tool-free (no fleece binary on the runner) and cross-platform — bash on Linux/macOS,
        // PowerShell on Windows — so it enforces "a mergeable branch has no live issues" everywhere.
        return """
name: Fleece CI Gate

on:
  pull_request:

permissions:
  contents: read

jobs:
  gate:
    name: No live Fleece issues
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Check .fleece/issues is empty (bash)
        if: runner.os != 'Windows'
        shell: bash
        run: |
          if ls .fleece/issues/*.jsonl >/dev/null 2>&1; then
            echo "::error::Live Fleece issues found under .fleece/issues/. Run 'fleece seal' to archive and clear them before merging."
            exit 1
          fi
          echo "No live Fleece issues. Gate passed."

      - name: Check .fleece/issues is empty (PowerShell)
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          $files = Get-ChildItem -Path '.fleece/issues' -Filter '*.jsonl' -File -ErrorAction SilentlyContinue
          if ($files) {
            Write-Output "::error::Live Fleece issues found under .fleece/issues/. Run 'fleece seal' to archive and clear them before merging."
            exit 1
          }
          Write-Output "No live Fleece issues. Gate passed."
""";
    }
}
