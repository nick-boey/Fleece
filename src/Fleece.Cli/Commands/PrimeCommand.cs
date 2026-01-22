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

            ## Available Commands

            - `fleece create --title "..." --type task|bug|chore|feature [-p PRIORITY] [-d "description"] [--previous ISSUES]`
              Create a new issue

            - `fleece list [--status STATUS] [--type TYPE] [-p PRIORITY]`
              List issues with optional filters

            - `fleece edit <ID> [--status STATUS] [--title "..."] [-p PRIORITY] [--linked-pr PR#]`
              Update an existing issue

            - `fleece delete <ID>`
              Delete an issue

            - `fleece search "query"`
              Search issues by text

            - `fleece diff`
              Show current conflicts

            - `fleece merge`
              Find and resolve duplicate issues

            - `fleece clear-conflicts <ID>`
              Clear conflict records for an issue

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

            ## Workflow Tips

            1. When starting work on an issue, update status: `fleece edit <ID> --status progress`
            2. When work is ready for review: `fleece edit <ID> --status review`
            3. When completing work: `fleece edit <ID> --status complete`
            4. Link PRs to issues: `fleece edit <ID> --linked-pr 123`
            5. Create follow-up issues as needed
            6. Use `--previous` to indicate order dependencies: `fleece create --title "B" --previous "A"`

            ## Questions

            Ask clarifying questions on issues:
            - `fleece question <ID> --ask "What is the expected behavior?"`
            - `fleece question <ID> --list` to see all questions
            - `fleece question <ID> --answer <Q-ID> --text "The expected behavior is..."`

            ## JSON Output

            Add `--json` to any list/search command for machine-readable output:
            `fleece list --json`
            """;

        Console.WriteLine(instructions);
        return 0;
    }
}
