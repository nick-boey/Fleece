using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fleece.Cli.Settings;
using Fleece.Core.Models;
using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Commands;

public sealed class QuestionCommand(IIssueService issueService) : AsyncCommand<QuestionSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, QuestionSettings settings)
    {
        // Get the issue first
        var issue = await issueService.GetByIdAsync(settings.Id);
        if (issue is null)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Issue '{settings.Id}' not found");
            return 1;
        }

        // Determine which action to take
        if (settings.List)
        {
            return ListQuestions(issue, settings.Json);
        }

        if (!string.IsNullOrWhiteSpace(settings.Ask))
        {
            return await AskQuestionAsync(issue, settings.Ask, settings.Json);
        }

        if (!string.IsNullOrWhiteSpace(settings.Answer))
        {
            if (string.IsNullOrWhiteSpace(settings.Text))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --text is required when using --answer");
                return 1;
            }

            return await AnswerQuestionAsync(issue, settings.Answer, settings.Text, settings.Json);
        }

        // Default to listing if no action specified
        return ListQuestions(issue, settings.Json);
    }

    private static int ListQuestions(Issue issue, bool json)
    {
        if (json)
        {
            var jsonOutput = JsonSerializer.Serialize(issue.Questions, FleeceJsonContext.Default.IReadOnlyListQuestion);
            Console.WriteLine(jsonOutput);
            return 0;
        }

        if (issue.Questions.Count == 0)
        {
            AnsiConsole.MarkupLine($"[dim]No questions on issue {issue.Id}[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold]Questions on {issue.Id}:[/]");
        AnsiConsole.WriteLine();

        foreach (var q in issue.Questions)
        {
            var answeredStatus = q.Answer is not null ? "[green]✓[/]" : "[yellow]?[/]";
            AnsiConsole.MarkupLine($"{answeredStatus} [bold]{q.Id}[/]: {Markup.Escape(q.Text)}");

            if (q.AskedBy is not null)
            {
                AnsiConsole.MarkupLine($"   [dim]Asked by {q.AskedBy} at {q.AskedAt:g}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"   [dim]Asked at {q.AskedAt:g}[/]");
            }

            if (q.Answer is not null)
            {
                AnsiConsole.MarkupLine($"   [green]A:[/] {Markup.Escape(q.Answer)}");
                if (q.AnsweredBy is not null)
                {
                    AnsiConsole.MarkupLine($"   [dim]Answered by {q.AnsweredBy} at {q.AnsweredAt:g}[/]");
                }
            }

            AnsiConsole.WriteLine();
        }

        return 0;
    }

    private async Task<int> AskQuestionAsync(Issue issue, string questionText, bool json)
    {
        var questionId = GenerateQuestionId(questionText);
        var now = DateTimeOffset.UtcNow;

        var newQuestion = new Question
        {
            Id = questionId,
            Text = questionText,
            AskedAt = now
        };

        var updatedQuestions = issue.Questions.ToList();
        updatedQuestions.Add(newQuestion);

        try
        {
            var updated = await issueService.UpdateQuestionsAsync(issue.Id, updatedQuestions);

            if (json)
            {
                var jsonOutput = JsonSerializer.Serialize(newQuestion, FleeceJsonContext.Default.Question);
                Console.WriteLine(jsonOutput);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Added question[/] [bold]{questionId}[/] to issue {issue.Id}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<int> AnswerQuestionAsync(Issue issue, string questionId, string answerText, bool json)
    {
        var questionIndex = issue.Questions.ToList().FindIndex(q =>
            q.Id.Equals(questionId, StringComparison.OrdinalIgnoreCase));

        if (questionIndex < 0)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Question '{questionId}' not found on issue {issue.Id}");
            return 1;
        }

        var existingQuestion = issue.Questions[questionIndex];
        var now = DateTimeOffset.UtcNow;

        var answeredQuestion = existingQuestion with
        {
            Answer = answerText,
            AnsweredAt = now
        };

        var updatedQuestions = issue.Questions.ToList();
        updatedQuestions[questionIndex] = answeredQuestion;

        try
        {
            var updated = await issueService.UpdateQuestionsAsync(issue.Id, updatedQuestions);

            if (json)
            {
                var jsonOutput = JsonSerializer.Serialize(answeredQuestion, FleeceJsonContext.Default.Question);
                Console.WriteLine(jsonOutput);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Answered question[/] [bold]{questionId}[/] on issue {issue.Id}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static string GenerateQuestionId(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text + DateTimeOffset.UtcNow.Ticks));
        var hashString = Convert.ToHexString(hash);
        return "Q" + hashString[..5];
    }
}
