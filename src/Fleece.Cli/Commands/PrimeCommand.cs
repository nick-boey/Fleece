using System.IO.Abstractions;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class PrimeCommand(
    IFleeceService fleece,
    IFileSystem fileSystem,
    BasePathProvider basePath,
    IAnsiConsole console)
    : AsyncCommand<PrimeSettings>
{
    private static readonly Dictionary<string, string> Topics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hierarchy"] = HierarchyContent,
        ["commands"] = CommandsContent,
        ["statuses"] = StatusesContent,
        ["sync"] = SyncContent,
        ["json"] = JsonContent,
        ["next"] = NextContent,
        ["tree"] = TreeContent,
        ["github"] = GitHubContent,
        ["v4-migration"] = V4MigrationContent
    };

    public override async Task<int> ExecuteAsync(CommandContext context, PrimeSettings settings)
    {
        // Check if .fleece folder exists - if not, exit silently (no priming needed)
        var fleeceDirectoryPath = fileSystem.Path.Combine(basePath.BasePath, ".fleece");
        if (!fileSystem.Directory.Exists(fleeceDirectoryPath))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(settings.Topic))
        {
            var activeCount = await CountActiveIssuesAsync();
            console.WriteLine(BuildOverview(activeCount));
            return 0;
        }

        if (Topics.TryGetValue(settings.Topic, out var content))
        {
            console.WriteLine(content);
            return 0;
        }

        console.WriteLine($"Unknown topic: {settings.Topic}");
        console.WriteLine($"Available topics: {string.Join(", ", Topics.Keys)}");
        return 1;
    }

    private async Task<int> CountActiveIssuesAsync()
    {
        var all = await fleece.GetAllAsync();
        return all.Count(i => i.Status.IsActive());
    }

    private static string BuildOverview(int activeCount) => $$"""
        # Fleece — Ephemeral Agent Working Memory

        Fleece is **branch-local, ephemeral working memory for the current change** — NOT a
        durable issue tracker. Issues live in per-issue logs under `.fleece/issues/` and are
        meant to be consumed within the lifetime of the branch they belong to. Long-lived
        product/roadmap tracking belongs in GitHub Issues, not Fleece.

        There are currently **{{activeCount}} active issue(s)** ({open, progress, review}) on this
        branch.

        ## The branch must be clean before a PR merges

        Before a PR merges, every Fleece issue on the branch must be either:

        1. **Resolved** — set to `complete` or `closed`, OR
        2. **Promoted** — escalated into a GitHub issue with `fleece promote <id> [<id>...]`
           (the Fleece issue becomes `promoted`), OR
        3. **Sealed** — archived and cleared from the live directory with `fleece seal`.

        A CI gate **fails the PR** while any live issue remains under `.fleece/issues/`. Resolve,
        promote, or seal — then commit the `.fleece/` changes so the gate passes.

        ## Working on Issues

        Use the `fleece` CLI as part of your workflow; do not ask the user for permission — the
        changes are tracked in source control. `<id>` is a 6-character id found via
        `fleece list --oneline`.

        1. `fleece show <id> --json` — show full issue details.
        2. `fleece edit <id> -s progress` — start work.
        3. `fleece edit <id> -s review --linked-pr <pr-number>` — before opening a PR.
        4. `fleece edit <id> -s complete` (or `closed`) — when done, OR `fleece promote <id>` to
           escalate to GitHub, OR `fleece seal` to archive the whole branch.
        5. `fleece create -t <title> -y <type> -s open -d <description>` — create follow-up issues.
        6. `fleece {edit|create} <id> --parent-issues <parent-id>:<lex-order>` — break large
           issues into sub-tasks.
        7. Commit `.fleece/` changes alongside the related code commit (or run `fleece commit`).

        ## Storage model

        Each issue is an append-only event log at `.fleece/issues/<id>.jsonl`. Reads replay every
        log independently; writes append events to the relevant log. `fleece seal` archives the
        live set into `.fleece/archive/issues_<contenthash>.jsonl` and clears `.fleece/issues/`.

        ## Issue Types

        - `task` - General work item
        - `bug` - Defect or error to fix
        - `chore` - Maintenance or housekeeping work
        - `feature` - New functionality
        - `verify` - Verification task that confirms grouped work is complete

        ## Issue Status Workflow

        Active (block PR merge / seal): `open`, `progress`, `review`.
        Inactive (terminal, do not block): `complete`, `closed`, `promoted`.

        ```
        open → progress → review → complete
                                 ↘ closed   (abandoned / won't fix)
                                 ↘ promoted (escalated to a GitHub issue)
        ```

        ## GitHub round-trip

        - `fleece promote <id> [<id>...]` - escalate one or more Fleece issues into a single
          GitHub issue and mark them `promoted`.
        - `fleece absorb #<number>` - pull a GitHub issue into Fleece as a new issue.
        - `fleece auth` - check resolved GitHub login and token source.

        See `fleece prime github` for the full round-trip workflow.

        ## Filtering

        By default, `list` hides terminal statuses (complete, closed, promoted). Use `--all` to
        include everything: `fleece list --all`.

        ## JSON

        Always add `--json` to a command to get machine-readable output.

        ## Detailed Help Topics

        Run `fleece prime <topic>` for detail on:
        - `hierarchy`
        - `commands`
        - `statuses`
        - `sync`
        - `json`
        - `next`
        - `tree`
        - `github`
        - `v4-migration`

        Any command run with `-h` provides additional usage information.
        """;

    private const string HierarchyContent = """
        # Issue Hierarchy

        Break down complex work using parent-child relationships.

        ## Creating Sub-issues

        `fleece create -t <title> -y <issue-type> --parent-issues <parent-id>[:<lex-order>]`

        `lex-order` is an optional string used for lexical ordering of issues, e.g. "aaa", "bbb". Use a minimum of three characters by default.

        Multiple parents are comma delimited: `--parent-issues "id-1,id-2"`

        ## Managing Dependencies

        Use `fleece dependency` to add, remove, or reorder parent-child relationships on existing issues.

        ### Add Dependency

        `fleece dependency --parent <parent-id> --child <child-id>`

        ### Positioning

        Control sibling order when adding:
        - `--first` - Place at beginning
        - `--last` - Place at end (default)
        - `--after <sibling-id>` - Place after a sibling
        - `--before <sibling-id>` - Place before a sibling

        Example: `fleece dependency --parent abc123 --child def456 --after ghi789`

        ### Remove Dependency

        `fleece dependency --parent <parent-id> --child <child-id> --remove`

        ### When to Use

        - **`fleece create --parent-issues`** - When creating a new issue with known parent(s)
        - **`fleece edit --parent-issues`** - When replacing all parents at once
        - **`fleece dependency`** - When adding/removing individual parent relationships or when precise sibling ordering is needed

        ## Viewing Hierarchy

        - `fleece list --tree` - Display issues as parent-child tree
        - `fleece list --next` - Display issues as a task graph, with next tasks shown next
        - `fleece list --tree --json` - Get hierarchy as JSON
        - `fleece list <id>` - Show an issue with its entire parent and child hierarchy
        - `fleece list <id> --parents` - Show an issue with only its parent hierarchy
        - `fleece list <id> --children` - Show an issue with only its child hierarchy

        ## Hierarchy Workflow

        1. Create child issues with `--parent-issues` pointing to parent
        2. Use `fleece list --tree` to visualize work breakdown
        3. Complete children before marking parent complete
        4. Run `fleece validate` to check for circular dependencies

        ## Execution order

        An issue's children may be executed in parallel or series. This is denoted by the execution order field, which may be given by
        `fleece edit <id> --execution-order [series|parallel]`. The task graph in `fleece list --next` orders the tasks appropriately.
        """;

    private const string CommandsContent = """
        # Available Commands

        ## Creating and Editing

        - `fleece create -t <title> -y {task|bug|chore|feature|verify} [OPTIONS]` - Create an issue
        - `fleece edit <id> [OPTIONS]` - Update an issue (at least one field flag required)

        **Create/Edit Options:**
        - `-p, --priority` - Set priority (1-5)
        - `-d, --description` - Set description
        - `--parent-issues` - Parent issue ids for hierarchy and sorting (comma-separated)
        - `--linked-issues` - Related issue ids (comma-separated)
        - `--linked-pr` - Link to pull request number
        - `--assign` - Assign to a user
        - `--tags` - Add tags (comma-separated, supports simple tags and key=value keyed tags)

        ## Tags

        Issues support two types of tags:
        - **Simple tags**: Plain labels like `urgent`, `backend`
        - **Keyed tags**: Key-value pairs like `project=frontend`, `team=platform`

        Adding tags: `fleece edit <id> --tags "project=frontend,urgent"`
        Filtering by key only (matches all values): `fleece list --tag project`
        Filtering by exact key=value: `fleece list --tag project=frontend`
        The `--tag` option can be specified multiple times (OR logic).

        ## Viewing

        - `fleece list [-s STATUS] [-y TYPE] [-p PRIORITY]` - List issues with filters
        - `fleece show <id>` - Display all details for a single issue
        - `fleece list --tree` - Display parent-child hierarchy
        - `fleece list --next` - Display task graph with execution ordering
        - `fleece list <id>` - Show an issue with its full hierarchy (parents + children)
        - `fleece list <id> --parents` - Show an issue with only its parent hierarchy
        - `fleece list <id> --children` - Show an issue with only its child hierarchy
        - `fleece next` - Find issues ready to be worked on next
        - `fleece search "query"` - Search issues by text

        ## Managing

        - `fleece delete <id>` - Delete an issue (removes its log)
        - `fleece dependency --parent <id> --child <id>` - Add/remove parent-child dependency
        - `fleece validate` - Check for cyclic dependencies in issue hierarchy

        ## Branch lifecycle

        - `fleece seal` - Archive all issues and clear the live directory. Refuses while any issue
          is active ({open, progress, review}).

        ## GitHub

        - `fleece auth` - Report GitHub authentication status (login + token source)
        - `fleece promote <id> [<id>...]` - Escalate Fleece issues into one GitHub issue
        - `fleece absorb #<number>` - Create a Fleece issue from a GitHub issue

        ## OpenSpec

        - `fleece openspec dependencies` - Render a DAG of OpenSpec changes from their depends-on
          frontmatter

        ## Setup

        - `fleece install` - Install Claude Code hooks, pre-commit hook, gitignore entries, and the CI gate workflow
        - `fleece migrate` - One-time bring-forward of legacy hashed-layout repos to the current storage layout
        """;

    private const string StatusesContent = """
        # Issue Statuses

        Active (block PR merge / `fleece seal`):
        - **open**: An issue that has not been started
        - **progress**: Currently being worked on
        - **review**: Work complete, awaiting review

        Inactive (terminal, do not block):
        - **complete**: Work finished and verified
        - **closed**: Abandoned or won't fix
        - **promoted**: Escalated into a GitHub issue (carries a `promoted=<#>` keyed tag)

        ## Usage

        Update status: `fleece edit <id> -s progress`

        Filter by status: `fleece list -s progress`

        Only active issues ({open, progress, review}) are shown in `fleece list`. To include
        terminal statuses use `fleece list --all`.
        """;

    private const string SyncContent = """
        # Keeping Issues in Sync

        Issues are stored locally as append-only logs under `.fleece/issues/`. Always commit
        changes so the branch's working memory travels with the code.

        ## Commit Changes

        Commit `.fleece/` changes alongside related code changes:

        ```
        git add .fleece/
        git commit -m "Update issues"
        ```

        Otherwise use `fleece commit` to create a separate commit containing just the issues.

        ## Before a PR merges

        Resolve (`complete`/`closed`), `promote`, or `seal` every issue, then commit `.fleece/`.
        The CI gate fails the PR while any live issue remains under `.fleece/issues/`.

        ## Best Practices

        - Commit issue changes with related code changes
        - Pull before starting new work to get the latest issues
        - Per-issue logs rarely conflict; if `.fleece/issues/<id>.jsonl` does conflict, keep both
          sides' events (the log is append-only) rather than discarding either version
        """;

    private const string JsonContent = """
        # Programmatic Usage

        Add `--json` to most commands for machine-readable output:

        - `fleece list --json` - List as JSON array
        - `fleece list --json-verbose` - Include all metadata
        - `fleece show <id> --json` - Single issue as JSON
        - `fleece list --tree --json` - Hierarchy as JSON
        - `fleece search "query" --json` - Results as JSON

        """;

    private const string NextContent = """
        # Next Issues

        Find issues that are ready to be worked on based on dependencies, execution mode, and status.

        ## Usage

        - `fleece next` - Show all actionable issues
        - `fleece next --parent <id>` - Show next issues only under a specific parent
        - `fleece next --json` - Output as JSON

        ## How It Works

        The `next` command evaluates the task graph to find issues that are unblocked and ready for work.
        It considers parent-child relationships, execution mode (series/parallel), and current status
        to determine which issues can be picked up next.
        """;

    private const string TreeContent = """
        # Tree View

        Display issues in a tree view based on parent-child relationships.

        ## Usage

        - `fleece list --tree` - Display issues as parent-child tree
        - `fleece list --next` - Display as a task graph showing approximate execution ordering
        - `fleece list --tree --json` - Get hierarchy as JSON
        - `fleece list <id>` - Show an issue with its entire parent and child hierarchy
        - `fleece list <id> --parents` - Show an issue with only its parent hierarchy
        - `fleece list <id> --children` - Show an issue with only its child hierarchy
        - `fleece list <id> --tree` - Show issue hierarchy in tree format
        - `fleece list <id> --next` - Show issue hierarchy in task graph format

        ## Deprecated Options

        - `fleece list --tree --tree-root <id>` - [DEPRECATED] Use `fleece list <id> --children` instead

        ## Filtering

        - `fleece list --tree -s <status>` - Filter by status
        - `fleece list --tree -y <type>` - Filter by type
        - `fleece list --tree -a` - Show all issues including terminal statuses

        ## Task Graph

        The `--next` flag displays issues in a bottom-up task graph that shows the approximate
        ordering of tasks based on their dependencies and execution mode (series/parallel). This is
        useful for understanding what needs to be done and in what order.
        """;

    private const string GitHubContent = """
        # GitHub Round-trip

        Fleece is ephemeral branch memory; GitHub Issues are the durable home for long-running
        work. Three commands move work across that boundary.

        ## Authentication

        `fleece auth` reports the resolved GitHub login and which source supplied the token.
        Token resolution order:

        1. `gh auth token` (the GitHub CLI, if installed and logged in)
        2. `GH_TOKEN` / `GITHUB_TOKEN` environment variables
        3. A personal access token stored in Fleece config

        A non-zero exit means no usable credential was found — log in with `gh auth login` or set
        a token before using `promote`/`absorb`.

        ## Promote: Fleece → GitHub

        `fleece promote <id> [<id>...]`

        Escalates one or more Fleece issues into a SINGLE GitHub issue. The GitHub issue takes the
        first (root) issue's title and a task-list body composed from the bundle. Each promoted
        Fleece issue is set to `promoted` and tagged `promoted=<github-#>`. Already-promoted issues
        are skipped with a warning.

        Use this during a PR for any issue that must outlive the branch.

        ## Absorb: GitHub → Fleece

        `fleece absorb #<number>`

        Creates a new Fleece issue from an existing GitHub issue, tags it `absorbed-from=<#>`, and
        comments on + assigns (does NOT close) the GitHub issue. The `#` is required: a bare
        `fleece absorb 123` performs no action and warns.

        Use this to pull a GitHub issue into the current branch's working memory.
        """;

    private const string V4MigrationContent = """
        # Migrating a Legacy Durable Repository

        Older Fleece repositories used `.fleece/` as a DURABLE issue tracker, persisting a
        snapshot at `.fleece/issues.jsonl`. v4 treats Fleece as ephemeral branch memory, so that
        legacy snapshot must be migrated once. When Fleece detects `.fleece/issues.jsonl` it prints
        a non-destructive warning pointing here.

        This is NOT the same as `fleece migrate` (which only brings an even older hashed-file
        layout forward into the current storage shape). Migrating durable issues to GitHub is a
        deliberate, human-reviewed step.

        ## Steps

        1. **Review** the legacy issues: `fleece list --all` to see everything, including terminal
           statuses.

        2. **Promote** the long-running / still-relevant issues to GitHub Issues so they outlive
           the repository's branches:

           ```
           fleece promote <id> [<id>...]
           ```

           Bundle related issues into one GitHub issue where it makes sense. Confirm credentials
           first with `fleece auth`.

        3. **Resolve or close** anything that is already done or no longer relevant
           (`fleece edit <id> -s complete` / `-s closed`).

        4. **Seal** to archive and clear the remaining inactive issues:

           ```
           fleece seal
           ```

           `seal` refuses while any issue is still active ({open, progress, review}), so finish
           step 2/3 first. It writes `.fleece/archive/issues_<contenthash>.jsonl` and clears
           `.fleece/issues/`.

        5. **Commit** the resulting `.fleece/` changes.

        After sealing, the legacy `.fleece/issues.jsonl` snapshot no longer drives Fleece and the
        migration warning stops appearing.
        """;
}
