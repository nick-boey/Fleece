using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

/// <summary>
/// Base class for command settings that support specifying a custom issues file.
/// </summary>
public abstract class FleeceCommandSettings : CommandSettings
{
    [CommandOption("-i|--issues <FILE>")]
    [Description("Path to a JSONL issues file to use instead of the default .fleece/issues_*.jsonl")]
    public string? IssuesFile { get; init; }
}
