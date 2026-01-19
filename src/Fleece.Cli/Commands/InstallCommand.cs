using System.Text.Json;
using System.Text.Json.Nodes;
using Fleece.Cli.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class InstallCommand : Command<InstallSettings>
{
    private const string ClaudeDirectory = ".claude";
    private const string SettingsFileName = "settings.json";

    public override int Execute(CommandContext context, InstallSettings settings)
    {
        var claudeDir = Path.Combine(Directory.GetCurrentDirectory(), ClaudeDirectory);
        var settingsPath = Path.Combine(claudeDir, SettingsFileName);

        Directory.CreateDirectory(claudeDir);

        JsonObject root;

        if (File.Exists(settingsPath))
        {
            var existingContent = File.ReadAllText(settingsPath);
            root = JsonNode.Parse(existingContent)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        // Add or update hooks
        var hooks = root["hooks"]?.AsObject() ?? new JsonObject();

        // Add preToolUse hook for fleece prime
        var preToolUseHooks = hooks["PreToolUse"]?.AsArray() ?? new JsonArray();
        var primeHook = new JsonObject
        {
            ["matcher"] = "*",
            ["hooks"] = new JsonArray { "fleece prime" }
        };

        // Check if hook already exists
        var hasPrimeHook = false;
        foreach (var hook in preToolUseHooks)
        {
            if (hook?["hooks"]?.AsArray()?.Any(h => h?.ToString() == "fleece prime") == true)
            {
                hasPrimeHook = true;
                break;
            }
        }

        if (!hasPrimeHook)
        {
            preToolUseHooks.Add(primeHook);
        }

        hooks["PreToolUse"] = preToolUseHooks;
        root["hooks"] = hooks;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(settingsPath, root.ToJsonString(options));

        AnsiConsole.MarkupLine("[green]Claude Code hooks installed successfully![/]");
        AnsiConsole.MarkupLine($"[dim]Settings written to: {settingsPath}[/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("The following hooks were configured:");
        AnsiConsole.MarkupLine("  [bold]PreToolUse:[/] fleece prime (provides issue context)");

        return 0;
    }
}
