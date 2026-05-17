using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class LinkSettings : CommandSettings
{
    [CommandOption("--merge")]
    [Description("Write a merge marker change file linking the current DAG leaves. " +
        "No-op if .git/MERGE_HEAD is absent. Intended for invocation from " +
        "pre-merge-commit / pre-commit hooks but also runnable manually.")]
    public bool Merge { get; init; }
}
