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

            - `fleece create --title "..." --type task|bug|chore|idea|feature [-p PRIORITY] [-d "description"]`
              Create a new issue

            - `fleece list [--status open|complete|closed|archived] [--type TYPE] [-p PRIORITY]`
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
            - idea: Future consideration
            - feature: New functionality

            ## Issue Statuses
            - open: Active, needs work
            - complete: Work finished, pending verification
            - closed: Done and verified
            - archived: No longer relevant

            ## Workflow Tips

            1. When starting work on an issue, note the ID
            2. When completing work, mark the issue complete: `fleece edit <ID> --status complete`
            3. Link PRs to issues: `fleece edit <ID> --linked-pr 123`
            4. Create follow-up issues as needed

            ## JSON Output

            Add `--json` to any list/search command for machine-readable output:
            `fleece list --json`
            """;

        Console.WriteLine(instructions);
        return 0;
    }
}
