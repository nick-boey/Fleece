using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Workflows;

/// <summary>
/// The Linear durable-tracker workflow. Linear is <em>agent-realized</em>: the fleece CLI never
/// calls Linear (its MCP tooling is reachable only by the agent). This workflow performs only local
/// operations — emitting a payload, recording an agent-supplied reference, or printing guidance —
/// and makes zero network calls, so it is exercised without any GitHub credential or fake service.
/// </summary>
public sealed class LinearTrackerWorkflow(
    IFleeceService fleeceService,
    IAnsiConsole console) : ITrackerWorkflow
{
    // Linear accepts both bare (emit) and --ref (record) promote, and never authenticates, so there
    // is nothing to gate before ID resolution.
    public Task<int?> PreparePromoteAsync(PromotePreflight preflight, CancellationToken cancellationToken = default)
        => Task.FromResult<int?>(null);

    public async Task<int> PromoteAsync(PromoteContext context, CancellationToken cancellationToken = default)
    {
        var root = context.Bundle[0];
        var issueIds = context.Bundle.Select(i => i.Id).ToList();

        // Emit-then-record: a bare invocation emits the payload and changes no state; the agent
        // creates the Linear issue via its own tooling, then re-runs with --ref to record it.
        if (string.IsNullOrWhiteSpace(context.Ref))
        {
            if (context.Json)
            {
                console.WriteLine(JsonSerializer.Serialize(new
                {
                    title = root.Title,
                    body = context.Body,
                    issueIds,
                }));
                return 0;
            }

            // The body carries markdown checkboxes ("- [ ]"), so it must be written literally —
            // Spectre would otherwise try to parse the brackets as markup.
            console.MarkupLine("[bold]Promote to Linear[/] — create the issue in Linear from this payload, then re-run with [bold]--ref[/] to record it:");
            console.WriteLine();
            console.WriteLine($"Title: {root.Title}");
            console.WriteLine();
            console.WriteLine(context.Body);
            console.WriteLine();
            console.MarkupLine("[dim]Then record the Linear reference:[/]");
            console.WriteLine($"  fleece promote {string.Join(' ', issueIds)} --ref <linear-id-or-url>");
            return 0;
        }

        var reference = context.Ref.Trim();
        await TrackerWorkflow.RecordPromotionAsync(fleeceService, context.Bundle, reference, cancellationToken);

        if (context.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                tracker = Trackers.Linear,
                promoted = issueIds,
                reference,
            }));
            return 0;
        }

        console.MarkupLine($"[green]Recorded Linear promotion[/] [bold]{reference.EscapeMarkup()}[/] [green]on {issueIds.Count} issue(s):[/] {string.Join(", ", issueIds).EscapeMarkup()}");
        return 0;
    }

    public Task<int> AbsorbAsync(AbsorbContext context, CancellationToken cancellationToken = default)
    {
        var reference = context.Reference?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reference))
        {
            console.MarkupLine("[red]Error:[/] A Linear issue reference is required (e.g. ENG-42).");
            return Task.FromResult(1);
        }

        if (context.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                tracker = Trackers.Linear,
                reference,
                action = "guidance",
            }));
            return Task.FromResult(0);
        }

        // Linear absorb is agent-realized: the CLI cannot comment on or assign the Linear issue, so
        // it prints the recipe and leaves the Linear-side side-effects to the agent's MCP tooling.
        console.MarkupLine($"[bold]Absorb Linear issue[/] [bold]{reference.EscapeMarkup()}[/] [dim](the fleece CLI does not call Linear):[/]");
        console.WriteLine();
        console.MarkupLine("Create the Fleece issue from the Linear issue's title/description:");
        console.WriteLine($"  fleece create -t \"<title>\" -y task -d \"<description>\" --tag absorbed-from={reference}");
        console.WriteLine();
        console.MarkupLine("[dim]Then comment on and assign the Linear issue with your Linear tooling.[/]");
        return Task.FromResult(0);
    }

    public Task<int> AuthAsync(AuthContext context, CancellationToken cancellationToken = default)
    {
        if (context.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(new
            {
                tracker = Trackers.Linear,
                applicable = false,
            }));
            return Task.FromResult(0);
        }

        console.MarkupLine($"[green]Durable tracker:[/] [bold]{Trackers.Linear}[/]");
        console.MarkupLine("The fleece CLI does not authenticate to Linear — issue hand-off is performed by the agent's Linear tooling (MCP).");
        return Task.FromResult(0);
    }
}
