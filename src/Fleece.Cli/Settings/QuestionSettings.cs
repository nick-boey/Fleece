using System.ComponentModel;
using Spectre.Console.Cli;

namespace Fleece.Cli.Settings;

public sealed class QuestionSettings : CommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("Issue ID")]
    public string Id { get; init; } = null!;

    [CommandOption("--list")]
    [Description("List all questions on the issue")]
    public bool List { get; init; }

    [CommandOption("--ask <TEXT>")]
    [Description("Ask a new question")]
    public string? Ask { get; init; }

    [CommandOption("--answer <QUESTION_ID>")]
    [Description("Answer a question (use with --text)")]
    public string? Answer { get; init; }

    [CommandOption("--text <TEXT>")]
    [Description("Answer text (use with --answer)")]
    public string? Text { get; init; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; init; }
}
