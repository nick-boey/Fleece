using Fleece.Core.Models;
using Spectre.Console;

namespace Fleece.Cli.Output;

public static class DiagnosticFormatter
{
    /// <summary>
    /// Renders diagnostic warnings to the console.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to render.</param>
    /// <returns>True if any warnings were rendered.</returns>
    public static bool RenderDiagnostics(IAnsiConsole console, IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        var hasWarnings = false;

        foreach (var diagnostic in diagnostics)
        {
            if (!diagnostic.HasIssues)
            {
                continue;
            }

            hasWarnings = true;
            var fileName = Path.GetFileName(diagnostic.FilePath);

            if (diagnostic.SkippedRows > 0)
            {
                console.MarkupLine(
                    $"[yellow]Warning:[/] Found {diagnostic.TotalRows} rows in {Markup.Escape(fileName)} " +
                    $"but only {diagnostic.ParsedRows} could be parsed.");
            }

            if (diagnostic.UnknownProperties.Count > 0)
            {
                var properties = string.Join(", ", diagnostic.UnknownProperties.OrderBy(p => p));
                if (diagnostic.SkippedRows == 0)
                {
                    console.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(fileName)} contains unknown properties: {Markup.Escape(properties)}");
                }
                else
                {
                    console.MarkupLine($"  [dim]Unknown properties:[/] {Markup.Escape(properties)}");
                }
            }

            if (diagnostic.ParseErrors.Count > 0)
            {
                var errorsToShow = diagnostic.ParseErrors.Take(3).ToList();
                foreach (var error in errorsToShow)
                {
                    console.MarkupLine($"  [red]Parse error:[/] {Markup.Escape(error)}");
                }

                if (diagnostic.ParseErrors.Count > 3)
                {
                    console.MarkupLine($"  [dim]... and {diagnostic.ParseErrors.Count - 3} more error(s)[/]");
                }
            }
        }

        if (hasWarnings)
        {
            console.WriteLine();
        }

        return hasWarnings;
    }
}
