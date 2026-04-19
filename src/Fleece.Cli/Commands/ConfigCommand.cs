using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class ConfigCommand(ISettingsService settingsService, IAnsiConsole console) : AsyncCommand<ConfigSettings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override async Task<int> ExecuteAsync(CommandContext context, ConfigSettings settings)
    {
        // Path mode: show settings file path
        if (settings.Path)
        {
            var path = settings.Global
                ? settingsService.GetGlobalSettingsPath()
                : settingsService.GetLocalSettingsPath();
            Console.WriteLine(path);
            return 0;
        }

        // List mode: show all effective settings
        if (settings.List)
        {
            var effective = await settingsService.GetEffectiveSettingsAsync();

            if (settings.Json)
            {
                RenderEffectiveSettingsJson(effective);
            }
            else
            {
                RenderEffectiveSettingsTable(effective);
            }

            return 0;
        }

        // Set mode: key=value
        if (!string.IsNullOrEmpty(settings.Set))
        {
            var parts = settings.Set.Split('=', 2);
            if (parts.Length != 2)
            {
                console.MarkupLine("[red]Error:[/] Invalid format. Use: --set key=value");
                return 1;
            }

            try
            {
                await settingsService.SetSettingAsync(parts[0], parts[1], settings.Global);
                var scope = settings.Global ? "global" : "local";
                console.MarkupLine($"[green]Set[/] {parts[0]} = {parts[1]} [dim]({scope})[/]");
                return 0;
            }
            catch (ArgumentException ex)
            {
                console.MarkupLine($"[red]Error:[/] {ex.Message}");
                return 1;
            }
        }

        // Get mode: show single setting
        if (!string.IsNullOrEmpty(settings.Get))
        {
            var effective = await settingsService.GetEffectiveSettingsAsync();
            var (value, source) = settings.Get.ToLowerInvariant() switch
            {
                "automerge" => (effective.AutoMerge.ToString().ToLowerInvariant(), effective.Sources.AutoMerge),
                "identity" => (effective.Identity ?? "", effective.Sources.Identity),
                "syncbranch" => (effective.SyncBranch ?? "", effective.Sources.SyncBranch),
                _ => (null, SettingSource.Default)
            };

            if (value is null)
            {
                console.MarkupLine($"[red]Error:[/] Unknown setting: {settings.Get}. Valid settings are: autoMerge, identity, syncBranch");
                return 1;
            }

            if (settings.Json)
            {
                var obj = new { value, source = source.ToString().ToLowerInvariant() };
                Console.WriteLine(JsonSerializer.Serialize(obj, JsonOptions));
            }
            else
            {
                Console.WriteLine(value);
            }

            return 0;
        }

        // No options: show usage
        console.MarkupLine("[bold]Usage:[/]");
        console.MarkupLine("  fleece config --list              Show all settings with sources");
        console.MarkupLine("  fleece config --get <key>         Get a setting value");
        console.MarkupLine("  fleece config --set <key>=<value> Set a local setting");
        console.MarkupLine("  fleece config --global --set ...  Set a global setting");
        console.MarkupLine("  fleece config --path              Show local settings file path");
        console.MarkupLine("  fleece config --global --path     Show global settings file path");
        console.MarkupLine("");
        console.MarkupLine("[bold]Available settings:[/]");
        console.MarkupLine("  autoMerge   Auto-merge issues before operations (true/false)");
        console.MarkupLine("  identity    User identity for ModifiedBy fields");
        console.MarkupLine("  syncBranch  Branch for issue synchronization");

        return 0;
    }

    private void RenderEffectiveSettingsTable(EffectiveSettings effective)
    {
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddColumn("Source");

        table.AddRow(
            "autoMerge",
            effective.AutoMerge.ToString().ToLowerInvariant(),
            FormatSource(effective.Sources.AutoMerge));

        table.AddRow(
            "identity",
            effective.Identity ?? "[dim](not set)[/]",
            FormatSource(effective.Sources.Identity));

        table.AddRow(
            "syncBranch",
            effective.SyncBranch ?? "[dim](not set)[/]",
            FormatSource(effective.Sources.SyncBranch));

        console.Write(table);
    }

    private static void RenderEffectiveSettingsJson(EffectiveSettings effective)
    {
        var output = new
        {
            autoMerge = new { value = effective.AutoMerge, source = effective.Sources.AutoMerge.ToString().ToLowerInvariant() },
            identity = new { value = effective.Identity, source = effective.Sources.Identity.ToString().ToLowerInvariant() },
            syncBranch = new { value = effective.SyncBranch, source = effective.Sources.SyncBranch.ToString().ToLowerInvariant() }
        };

        Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
    }

    private static string FormatSource(SettingSource source) => source switch
    {
        SettingSource.Default => "[dim]default[/]",
        SettingSource.Global => "[blue]global[/]",
        SettingSource.Local => "[green]local[/]",
        SettingSource.CommandLine => "[yellow]cli[/]",
        _ => source.ToString()
    };
}
