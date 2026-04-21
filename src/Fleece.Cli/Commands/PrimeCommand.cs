using Fleece.Cli.Settings;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class PrimeCommand : Command<PrimeSettings>
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
        ["merge"] = MergeContent,
        ["openspec"] = OpenSpecContent
    };

    public override int Execute(CommandContext context, PrimeSettings settings)
    {
        // Check if .fleece folder exists - if not, exit silently (no priming needed)
        var fleeceDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), ".fleece");
        if (!Directory.Exists(fleeceDirectoryPath))
        {
            return 0;
        }

        var openspecDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "openspec");
        var hasOpenSpec = Directory.Exists(openspecDirectoryPath);

        if (string.IsNullOrWhiteSpace(settings.Topic))
        {
            if (hasOpenSpec)
            {
                Console.WriteLine(OverviewContent);
                Console.WriteLine();
                Console.WriteLine(OpenSpecContent);
            }
            else
            {
                Console.WriteLine(OverviewContent);
            }
            return 0;
        }

        if (Topics.TryGetValue(settings.Topic, out var content))
        {
            Console.WriteLine(content);
            return 0;
        }

        Console.WriteLine($"Unknown topic: {settings.Topic}");
        Console.WriteLine($"Available topics: {string.Join(", ", Topics.Keys)}");
        return 1;
    }

    private const string OverviewContent = """
        # Fleece Issue Tracking

        This project uses a CLI tool named Fleece for local issue tracking. Issues are stored in JSONL files in the `.fleece/` folder of the repository.

        ## Working on Issues

        Use the `fleece` CLI tool as part of your workflow. Do not ask the user for permission to use the Fleece CLI tools
        as the changes are tracked in the repository's source control. <id> is a 6 character hash that can be found with `fleece list --oneline`.

        1. When given an issue ID to work on, use `fleece show <id> --json` to show the full issue details.

        2. When starting work on an issue, update the status to progress:
           `fleece edit <id> -s progress`

        3. **Before creating a PR**, update the issue and commit all fleece changes:
           `fleece edit <id> -s review --linked-pr <pr-number>`
           - Either include `.fleece/` changes with your code commits, OR
           - Use `fleece commit --ci` to commit and allow CI to run

        4. When completing work:
           `fleece edit <id> -s complete`

        5. Create follow-up issues as needed with `fleece create -t <title> -s open -y <type> -d <description>`

        6. Use `fleece {edit|create} <id> --parent-issues <parent-id>:<lex-order>` to break down large issues into sub-tasks

        7. Commit changes by including all changes in the `.fleece/` folder with related code commits or using the `fleece commit` command

        9. When encountering git merge conflicts in `.fleece/` files, ALWAYS use `fleece merge` to resolve them.
           Never delete conflicting files manually.

        ## Issue Types

        - `task` - General work item
        - `bug` - Defect or error to fix
        - `chore` - Maintenance or housekeeping work
        - `feature` - New functionality
        - `verify` - Verification task that confirms grouped work is complete

        ### Verify Issues

        The `verify` type is used to create a parent issue that groups related work together.
        When all child issues are complete, the verify issue serves as a checkpoint to confirm
        the work was done correctly. Use verify issues when:
        - Multiple related tasks need to be completed as a unit
        - You need a final review/confirmation step after completing sub-tasks
        - Work needs explicit sign-off before being considered done

        ## Issue Status Workflow

        Issues progress through statuses as work advances. Update status to reflect current state:

        ```
        open → progress → review → complete
                                 ↘ archived (no longer relevant)
                                 ↘ closed (abandoned/won't fix)
        ```

        ## Task Hierarchy

        Issues can be organized into parent-child hierarchies to break down complex work. Use `fleece list --tree` to view the
        hierarchy and `fleece list --next` to see the task graph with approximate execution ordering.

        - `fleece list --tree` - Display issues as parent-child tree
        - `fleece list --next` - Display issues as a task graph, showing execution order and next tasks
        - `fleece next` - Find issues that can be worked on next based on dependencies and execution mode

        Use `fleece create -t <title> -y <type> --parent-issues <parent-id>:<lex-order>` to create sub-tasks.
        Use `fleece dependency --parent <id> --child <id>` to manage dependencies on existing issues.

        ## Filtering

        By default, `list` hides terminal statuses (complete, archived, closed).
        Use `--all` to include all: `fleece list --all`

        ## JSON

        Always use `--json` after commands to get all output in machine readable JSON format.

        ## Detailed Help Topics

        Run `fleece prime <topic>` for detailed information on the following topics:
        - `hierarchy`
        - `commands`
        - `statuses`
        - `sync`
        - `json`
        - `next`
        - `tree`
        - `merge`
        - `openspec`

        Any command when run with `-h` will provide additional information on the command usage.
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

        - `fleece delete <id>` - Delete an issue
        - `fleece dependency --parent <id> --child <id>` - Add/remove parent-child dependency
        - `fleece validate` - Check for cyclic dependencies in issue hierarchy

        ## Collaboration

        - `fleece diff` - Show change history and conflicts
        - `fleece merge` - Find and resolve duplicate issues

        ## Setup

        - `fleece install` - Install Claude Code hooks
        """;

    private const string StatusesContent = """
        # Issue Statuses

        - **open**: An issue that has not been started
        - **progress**: Currently being worked on
        - **review**: Work complete, awaiting review
        - **complete**: Work finished and verified
        - **archived**: No longer relevant
        - **closed**: Abandoned or won't fix

        ## Usage

        Update status: `fleece edit <id> -s progress`

        Filter by status: `fleece list -s progress`

        Only issues that are non-terminal (e.g. `open`, `progress`, `review` are shown in `fleece list`.
        To show terminal statuses use `fleece list --all`.
        """;

    private const string SyncContent = """
        # Keeping Issues in Sync

        Issues are stored locally in JSONL files in `.fleece/`. Always commit changes to keep issues synchronized.

        ## Commit Changes

        Commit `.fleece/` changes alongside related code changes:

        ```
        git add .fleece/
        git commit -m "Update issues"
        ```

        Otherwise use `fleece commit` to create a separate commit containing just the issues.

        ## After Pulling

        Check `fleece diff` for conflicts, use `fleece merge` if needed.

        ## Best Practices

        - Commit issue changes with related code changes
        - Pull before starting new work to get latest issues
        - Use `fleece diff` to review changes before committing
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

    private const string MergeContent = """
        # Resolving Merge Conflicts in .fleece/

        When git merge conflicts occur in the `.fleece/` folder, you MUST use `fleece merge` to resolve them.
        DO NOT manually resolve conflicts by deleting one version of the file.

        ## Why Use fleece merge

        The `fleece merge` command intelligently merges conflicting issue data:
        - **Property-level merging**: Each field is merged individually, not whole files
        - **Timestamp-based resolution**: Scalar properties use the newer timestamp
        - **Collection union**: Tags, linked issues, and parent issues combine both versions
        - **No data loss**: Both versions contribute to the final merged result

        ## Handling Git Merge Conflicts

        When you encounter a merge conflict in `.fleece/` files:

        1. **Accept both versions** during git merge (keep both conflicting files)
        2. **Run `fleece merge`** to intelligently combine duplicates
        3. **Verify the merge** with `fleece list` or `fleece show <id>`
        4. **Commit the resolved files**

        ## Example Workflow

        ```bash
        # After a git merge/pull with conflicts in .fleece/
        git add .fleece/          # Stage all versions
        fleece merge              # Intelligently merge duplicates
        fleece list               # Verify issues look correct
        git add .fleece/          # Stage merged result
        git commit -m "Resolve fleece merge conflicts"
        ```

        ## Dry Run

        Preview what would be merged without making changes:
        ```
        fleece merge --dry-run
        ```

        ## IMPORTANT

        NEVER resolve .fleece/ conflicts by:
        - Deleting one version of an issue file
        - Manually editing JSONL files
        - Using git checkout --ours/--theirs

        ALWAYS use `fleece merge` which preserves data from both branches.
        """;

    private const string OpenSpecContent = """
        # OpenSpec Integration

        This repository uses OpenSpec (in `openspec/`) alongside Fleece. Fleece tracks
        *who/when* work happens; OpenSpec tracks *what* is being built. Link them so
        both stories stay aligned.

        ## Linking a Fleece issue to an OpenSpec change

        Use the keyed tag `openspec={change-name}` on the issue:

        ```
        fleece edit <id> --tags "openspec=my-change-name"
        ```

        Filter: `fleece list --tag openspec=my-change-name` (exact) or
        `fleece list --tag openspec` (any change).

        Multiple `openspec=` tags on one issue ARE permitted but discouraged — prefer
        one issue per change.

        ## Single-change session: which issue to link

        When proposing one new OpenSpec change, pick the Fleece issue to link using
        this decision tree:

        ```
        branch name ends in `+<id>` ?
        ├─ yes → look up issue <id>
        │        ├─ open AND no existing openspec= tag AND relevant to this change
        │        │    → link it (add openspec={change-name})
        │        └─ otherwise → fall through
        └─ no  → scan open issues for an unlinked, relevant match
                 ├─ one obvious candidate → link it
                 ├─ ambiguous             → ask the user which to link
                 └─ none                  → create a new issue as a last resort
        ```

        - "Relevant" means the issue's title/description matches the scope of the
          proposed change.
        - "Unlinked" means the issue has no existing `openspec=` tag.
        - Creating a new issue is the last resort, not the default.

        ## Multi-change session: hierarchy

        When one session proposes multiple OpenSpec changes, create exactly ONE Fleece
        issue per change and organise them using Fleece hierarchy features:

        - **One issue per change** — never more, never fewer.
        - **Flat fan-out is the default** — attach all per-change issues directly to
          the session's root issue with
          `fleece create ... --parent-issues <root-id>:<lex-order>`.
        - **Intermediate grouping parents** only when the hierarchy genuinely requires
          them (e.g. two changes that must complete before a third).
        - Use lex-order (e.g. `aaa`, `aab`) to sequence siblings and
          `fleece edit <root-id> --execution-order series|parallel` to signal whether
          the per-change issues must be sequenced.

        ## Never create issues per task or per phase

        An OpenSpec change already owns its own task list (`tasks.md`) and phase
        structure. Do NOT mirror those as Fleece issues. One Fleece issue per OpenSpec
        change is the correct granularity — never one per task, per phase, or per
        spec requirement.
        """;
}
