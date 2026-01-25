using Fleece.Cli.Settings;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class PrimeCommand : Command<PrimeSettings>
{
    public override int Execute(CommandContext context, PrimeSettings settings)
    {
        var instructions = """
            # Fleece Issue Tracking

            This project uses Fleece for local issue tracking. Issues are stored in `.fleece/issues.jsonl`.

            ## Setup

            Run `fleece install` to set up Claude Code hooks for automatic context loading.

            ## Available Commands

            ### Creating and Editing
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

            ### Viewing
            - `fleece list [--status STATUS] [--type TYPE] [-p PRIORITY]` - List issues with filters
            - `fleece show <ID>` - Display all details for a single issue
            - `fleece tree` - Display parent-child hierarchy
            - `fleece search "query"` - Search issues by text

            ### Managing
            - `fleece delete <ID>` - Delete an issue
            - `fleece validate` - Check for cyclic dependencies in issue hierarchy

            ### Collaboration
            - `fleece diff` - Show change history and conflicts
            - `fleece merge` - Find and resolve duplicate issues
            - `fleece clear-conflicts <ID>` - Clear conflict records for an issue

            ### Setup
            - `fleece install` - Install Claude Code hooks

            ## Issue Types
            - task: General work item
            - bug: Something broken
            - chore: Maintenance work
            - feature: New functionality

            ## Issue Statuses
            - idea: Initial concept, needs refinement
            - spec: Requirements defined, ready for planning
            - next: Prioritized for upcoming work
            - progress: Currently being worked on
            - review: Work complete, awaiting review
            - complete: Work finished and verified
            - archived: No longer relevant
            - closed: Abandoned or won't fix

            ## Issue Status Workflow

            Issues progress through statuses as work advances. Update status to reflect current state:

            ```
            idea → spec → next → progress → review → complete
                                                  ↘ archived (no longer relevant)
                                                  ↘ closed (abandoned/won't fix)
            ```

            **When to update status:**
            - `idea` → `spec`: When requirements are defined and documented
            - `spec` → `next`: When prioritized for upcoming work
            - `next` → `progress`: When actively working on the issue
            - `progress` → `review`: When work is complete and awaiting review
            - `review` → `complete`: When work is verified and merged

            **Alternative endings:**
            - Use `archived` when an issue becomes irrelevant (superseded, no longer needed)
            - Use `closed` when explicitly deciding not to do the work (won't fix, out of scope)

            ## Issue Hierarchy

            Break down complex work using parent-child relationships:

            ### Creating Sub-issues
            `fleece create --title "Implement API" --type task --parent-issues "PARENT-ID"`

            Multiple parents: `--parent-issues "ID1,ID2"`

            ### Viewing Hierarchy
            - `fleece tree` - Display issues as parent-child tree
            - `fleece tree --json` - Get hierarchy as JSON

            ### Hierarchy Workflow
            1. Create child issues with `--parent-issues` pointing to parent
            2. Use `fleece tree` to visualize work breakdown
            3. Complete children before marking parent complete
            4. Run `fleece validate` to check for circular dependencies

            ## Workflow Tips

            1. When starting work on an issue, update status: `fleece edit <ID> --status progress`
            2. When work is ready for review: `fleece edit <ID> --status review`
            3. When completing work: `fleece edit <ID> --status complete`
            4. Link PRs to issues: `fleece edit <ID> --linked-pr 123`
            5. Create follow-up issues as needed
            6. Use `--previous` to indicate order dependencies: `fleece create --title "B" --previous "A"`
            7. Use `--parent-issues` to break down large issues into sub-tasks
            8. Commit `.fleece/` changes with related code changes
            9. Run `fleece tree` to visualize work breakdown

            ## Questions

            Ask clarifying questions on issues:
            - `fleece question <ID> --ask "What is the expected behavior?"`
            - `fleece question <ID> --list` to see all questions
            - `fleece question <ID> --answer <Q-ID> --text "The expected behavior is..."`

            ## Keeping Issues in Sync

            Issues are stored locally in `.fleece/`. Always commit changes to keep issues synchronized:

            **Important:** Commit `.fleece/` changes alongside related code changes:
            ```
            git add .fleece/
            git commit -m "Update issues"
            ```

            After pulling: Check `fleece diff` for conflicts, use `fleece merge` if needed.

            ## Programmatic Usage

            Add `--json` to most commands for machine-readable output:
            - `fleece list --json` - List as JSON array
            - `fleece list --json-verbose` - Include all metadata
            - `fleece show <ID> --json` - Single issue as JSON
            - `fleece tree --json` - Hierarchy as JSON
            - `fleece search "query" --json` - Results as JSON

            ### Compact Output
            `fleece list --one-line` - Each issue on single line

            ### Filtering
            By default, `list` and `tree` hide terminal statuses (complete, archived, closed).
            Use `--all` to include all: `fleece list --all`
            """;

        Console.WriteLine(instructions);
        return 0;
    }
}
