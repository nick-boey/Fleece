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
        ["questions"] = QuestionsContent,
        ["next"] = NextContent,
        ["tree"] = TreeContent
    };

    public override int Execute(CommandContext context, PrimeSettings settings)
    {
        // Check if .fleece folder exists - if not, exit silently (no priming needed)
        var fleeceDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), ".fleece");
        if (!Directory.Exists(fleeceDirectoryPath))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(settings.Topic))
        {
            Console.WriteLine(OverviewContent);
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

        3. **Before creating a PR**, update the issue status and commit fleece changes:
           `fleece edit <id> -s review`
           - Either include `.fleece/` changes with your code commits, OR
           - Use `fleece commit --ci` to commit and allow CI to run

        4. After creating the PR, link the PR number:
           `fleece edit <id> --linked-pr <pr-number>`

        5. When completing work:
           `fleece edit <id> -s complete`

        6. Create follow-up issues as needed with `fleece create -t <title> -s open -y <type> -d <description>`

        7. Use `fleece {edit|create} <id> --parent-issues <parent-id>:<lex-order>` to break down large issues into sub-tasks

        8. Commit changes by including all changes in the `.fleece/` folder with related code commits or using the `fleece commit` command

        ## Issue Types

        - `task`
        - `bug`
        - `chore`
        - `feature`

        ## Issue Status Workflow

        Issues progress through statuses as work advances. Update status to reflect current state:

        ```
        open → progress → review → complete
                                 ↘ archived (no longer relevant)
                                 ↘ closed (abandoned/won't fix)
        ```

        ## Task Hierarchy

        Issues can be organized into parent-child hierarchies to break down complex work. Use `fleece tree` to view the
        hierarchy and `fleece tree --task-graph` to see the task graph with approximate execution ordering.

        - `fleece tree` - Display issues as parent-child tree
        - `fleece tree --task-graph` - Display issues as a task graph, showing execution order and next tasks
        - `fleece next` - Find issues that can be worked on next based on dependencies and execution mode

        Use `fleece create -t <title> -y <type> --parent-issues <parent-id>:<lex-order>` to create sub-tasks.

        ## Filtering

        By default, `list` and `tree` hide terminal statuses (complete, archived, closed).
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
        - `questions`
        - `next`
        - `tree`

        Any command when run with `-h` will provide additional information on the command usage.
        """;

    private const string HierarchyContent = """
        # Issue Hierarchy

        Break down complex work using parent-child relationships.

        ## Creating Sub-issues

        `fleece create -t <title> -y <issue-type> --parent-issues <parent-id>[:<lex-order>]`

        `lex-order` is an optional string used for lexical ordering of issues, e.g. "aaa", "bbb". Use a minimum of three characters by default.

        Multiple parents are comma delimited: `--parent-issues "id-1,id-2"`

        ## Viewing Hierarchy

        - `fleece tree` - Display issues as parent-child tree
        - `fleece tree --task-graph` - Display issues as a task graph, with next tasks shown next
        - `fleece tree --json` - Get hierarchy as JSON

        ## Hierarchy Workflow

        1. Create child issues with `--parent-issues` pointing to parent
        2. Use `fleece tree` to visualize work breakdown
        3. Complete children before marking parent complete
        4. Run `fleece validate` to check for circular dependencies

        ## Execution order

        An issue's children may be executed in parallel or series. This is denoted by the execution order field, which may be given by
        `fleece edit <id> --execution-order [series|parallel]`. The task graph in `fleece tree --task-graph` orders the tasks appropriately.
        """;

    private const string CommandsContent = """
        # Available Commands

        ## Creating and Editing

        - `fleece create` - Open interactive editor with YAML template
        - `fleece create -t <title> -y {task|bug|chore|feature} [OPTIONS]` - Create from command line
        - `fleece edit <id> [OPTIONS]` - Update from command line

        **Create/Edit Options:**
        - `-p, --priority` - Set priority (1-5)
        - `-d, --description` - Set description
        - `--parent-issues` - Parent issue ids for hierarchy and sorting (comma-separated)
        - `--linked-issues` - Related issue ids (comma-separated)
        - `--linked-pr` - Link to pull request number
        - `--assign` - Assign to a user
        - `--tags` - Add tags (comma-separated)

        ## Viewing

        - `fleece list [-s STATUS] [-y TYPE] [-p PRIORITY]` - List issues with filters
        - `fleece show <id>` - Display all details for a single issue
        - `fleece tree` - Display parent-child hierarchy
        - `fleece tree --task-graph` - Display task graph with execution ordering
        - `fleece next` - Find issues ready to be worked on next
        - `fleece search "query"` - Search issues by text

        ## Managing

        - `fleece delete <id>` - Delete an issue
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
        - `fleece tree --json` - Hierarchy as JSON
        - `fleece search "query" --json` - Results as JSON

        """;

    private const string QuestionsContent = """
        # Questions

        Ask clarifying questions on issues to gather requirements or resolve ambiguity.

        ## Ask a Question

        `fleece question <id> --ask "What is the expected behavior?"`

        ## List Questions

        `fleece question <id> --list`

        ## Answer a Question

        `fleece question <id> --answer <question-id> --text "The expected behavior is..."`
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

        - `fleece tree` - Display issues as parent-child tree
        - `fleece tree --task-graph` - Display as a task graph showing approximate execution ordering
        - `fleece tree --json` - Get hierarchy as JSON

        ## Filtering

        - `fleece tree -s <status>` - Filter by status
        - `fleece tree -y <type>` - Filter by type
        - `fleece tree -a` - Show all issues including terminal statuses

        ## Task Graph

        The `--task-graph` flag displays issues in a bottom-up task graph that shows the approximate
        ordering of tasks based on their dependencies and execution mode (series/parallel). This is
        useful for understanding what needs to be done and in what order.
        """;
}
