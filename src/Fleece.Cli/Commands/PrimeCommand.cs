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
        ["questions"] = QuestionsContent
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

        This project uses Fleece for local issue tracking. Issues are stored in `.fleece/issues.jsonl`.

        ## Issue Types
        - task, bug, chore, feature

        ## Issue Status Workflow

        Issues progress through statuses as work advances. Update status to reflect current state:

        ```
        idea → spec → next → progress → review → complete
                                              ↘ archived (no longer relevant)
                                              ↘ closed (abandoned/won't fix)
        ```

        ## Working on Issues

        1. When starting work on an issue, update status:
           `fleece edit <ID> --status progress`

        2. When work is ready for review:
           `fleece edit <ID> --status review`

        3. When completing work:
           `fleece edit <ID> --status complete`

        4. Link PRs to issues:
           `fleece edit <ID> --linked-pr 123`

        5. Create follow-up issues as needed

        6. Use `--previous` to indicate order dependencies:
           `fleece create --title "B" --previous "A"`

        7. Use `--parent-issues` to break down large issues into sub-tasks

        8. Commit `.fleece/` changes with related code changes

        9. Run `fleece tree` to visualize work breakdown

        ## Detailed Help Topics
        Run `fleece prime <topic>` for detailed information:

        - hierarchy, commands, statuses, sync, json, questions
        """;

    private const string HierarchyContent = """
        # Issue Hierarchy

        Break down complex work using parent-child relationships.

        ## Creating Sub-issues

        `fleece create --title "Implement API" --type task --parent-issues "PARENT-ID"`

        Multiple parents: `--parent-issues "ID1,ID2"`

        ## Viewing Hierarchy

        - `fleece tree` - Display issues as parent-child tree
        - `fleece tree --json` - Get hierarchy as JSON

        ## Hierarchy Workflow

        1. Create child issues with `--parent-issues` pointing to parent
        2. Use `fleece tree` to visualize work breakdown
        3. Complete children before marking parent complete
        4. Run `fleece validate` to check for circular dependencies
        """;

    private const string CommandsContent = """
        # Available Commands

        ## Creating and Editing

        - `fleece create` - Open interactive editor with YAML template
        - `fleece create --title "..." --type task|bug|chore|feature [OPTIONS]` - Create from command line
        - `fleece edit <ID>` - Open interactive editor for existing issue
        - `fleece edit <ID> [--status STATUS] [--title "..."] [OPTIONS]` - Update from command line

        **Create/Edit Options:**
        - `-p, --priority` - Set priority (1-5)
        - `-d, --description` - Set description
        - `--previous` - Issues that must complete first (comma-separated IDs)
        - `--parent-issues` - Parent issue IDs for hierarchy (comma-separated)
        - `--linked-issues` - Related issue IDs (comma-separated)
        - `--linked-pr` - Link to pull request number
        - `--group` - Categorize issue into a group
        - `--assign` - Assign to a user
        - `--tags` - Add tags (comma-separated)
        - `--working-branch` - Link to git branch

        ## Viewing

        - `fleece list [--status STATUS] [--type TYPE] [-p PRIORITY]` - List issues with filters
        - `fleece show <ID>` - Display all details for a single issue
        - `fleece tree` - Display parent-child hierarchy
        - `fleece search "query"` - Search issues by text

        ## Managing

        - `fleece delete <ID>` - Delete an issue
        - `fleece validate` - Check for cyclic dependencies in issue hierarchy

        ## Collaboration

        - `fleece diff` - Show change history and conflicts
        - `fleece merge` - Find and resolve duplicate issues
        - `fleece clear-conflicts <ID>` - Clear conflict records for an issue

        ## Setup

        - `fleece install` - Install Claude Code hooks
        """;

    private const string StatusesContent = """
        # Issue Statuses

        - **idea**: Initial concept, needs refinement
        - **spec**: Requirements defined, ready for planning
        - **next**: Prioritized for upcoming work
        - **progress**: Currently being worked on
        - **review**: Work complete, awaiting review
        - **complete**: Work finished and verified
        - **archived**: No longer relevant
        - **closed**: Abandoned or won't fix

        ## Usage

        Update status: `fleece edit <ID> --status progress`

        Filter by status: `fleece list --status progress`

        Show terminal statuses: `fleece list --all`
        """;

    private const string SyncContent = """
        # Keeping Issues in Sync

        Issues are stored locally in `.fleece/`. Always commit changes to keep issues synchronized.

        ## Commit Changes

        Commit `.fleece/` changes alongside related code changes:

        ```
        git add .fleece/
        git commit -m "Update issues"
        ```

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
        - `fleece show <ID> --json` - Single issue as JSON
        - `fleece tree --json` - Hierarchy as JSON
        - `fleece search "query" --json` - Results as JSON

        ## Compact Output

        `fleece list --one-line` - Each issue on single line

        ## Filtering

        By default, `list` and `tree` hide terminal statuses (complete, archived, closed).
        Use `--all` to include all: `fleece list --all`
        """;

    private const string QuestionsContent = """
        # Questions

        Ask clarifying questions on issues to gather requirements or resolve ambiguity.

        ## Ask a Question

        `fleece question <ID> --ask "What is the expected behavior?"`

        ## List Questions

        `fleece question <ID> --list`

        ## Answer a Question

        `fleece question <ID> --answer <Q-ID> --text "The expected behavior is..."`
        """;
}
