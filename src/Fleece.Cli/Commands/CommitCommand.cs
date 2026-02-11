using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class CommitCommand(IGitService gitService) : Command<CommitSettings>
{
    private const string DefaultCommitMessage = "Update fleece issues";

    public override int Execute(CommandContext context, CommitSettings settings)
    {
        // Check git availability
        if (!gitService.IsGitAvailable())
        {
            if (settings.Json)
            {
                OutputJsonError("git command not found. Please ensure git is installed and available in PATH.");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] git command not found. Please ensure git is installed and available in PATH.");
            }
            return 1;
        }

        // Check if in git repository
        if (!gitService.IsGitRepository())
        {
            if (settings.Json)
            {
                OutputJsonError("Not a git repository. Please run this command from within a git repository.");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Not a git repository. Please run this command from within a git repository.");
            }
            return 1;
        }

        // Check for changes
        if (!gitService.HasFleeceChanges())
        {
            if (settings.Json)
            {
                OutputJsonResult(committed: false, pushed: false, message: null, warning: "No changes to commit in .fleece directory.");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No changes to commit in .fleece directory.[/]");
            }
            return 0;
        }

        var message = settings.Message ?? DefaultCommitMessage;
        if (!settings.Ci)
        {
            message += " [skip ci]";
        }

        // Commit (and optionally push)
        var result = settings.Push
            ? gitService.CommitAndPushFleeceChanges(message)
            : gitService.CommitFleeceChanges(message);

        if (!result.Success)
        {
            if (settings.Json)
            {
                OutputJsonError(result.ErrorMessage ?? "Unknown error");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");
            }
            return 1;
        }

        // Success output
        if (settings.Json)
        {
            OutputJsonResult(committed: true, pushed: settings.Push, message: message);
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Committed:[/] {Markup.Escape(message)}");
            if (settings.Push)
            {
                AnsiConsole.MarkupLine("[green]Pushed to remote[/]");
            }
        }

        return 0;
    }

    private static void OutputJsonError(string error)
    {
        var result = new { success = false, error };
        AnsiConsole.WriteLine(JsonSerializer.Serialize(result));
    }

    private static void OutputJsonResult(bool committed, bool pushed, string? message, string? warning = null)
    {
        var result = new { success = true, committed, pushed, message, warning };
        AnsiConsole.WriteLine(JsonSerializer.Serialize(result));
    }
}
