using System.Security.Cryptography;
using System.Text;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Spectre.Console;

namespace Fleece.Cli.Tui.Screens;

/// <summary>
/// Sub-menu for asking and answering questions on an issue.
/// </summary>
public sealed class QuestionScreen(
    IIssueService issueService,
    IGitConfigService gitConfigService,
    IAnsiConsole console)
{
    public async Task<Issue> ShowAsync(Issue issue, CancellationToken cancellationToken)
    {
        var currentIssue = issue;

        while (true)
        {
            console.Clear();
            RenderQuestions(currentIssue);
            console.WriteLine();

            var choices = new List<string> { "Ask a Question" };

            var unanswered = (currentIssue.Questions ?? []).Where(q => q.Answer is null).ToList();
            if (unanswered.Count > 0)
            {
                choices.Add("Answer a Question");
            }

            choices.Add("<< Back");

            var choice = console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Questions:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));

            switch (choice)
            {
                case "Ask a Question":
                    currentIssue = await AskQuestionAsync(currentIssue, cancellationToken);
                    break;

                case "Answer a Question":
                    currentIssue = await AnswerQuestionAsync(currentIssue, cancellationToken);
                    break;

                case "<< Back":
                    return currentIssue;
            }
        }
    }

    private async Task<Issue> AskQuestionAsync(Issue issue, CancellationToken cancellationToken)
    {
        var questionText = console.Prompt(
            new TextPrompt<string>("[bold]Question text:[/]")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(questionText))
        {
            console.MarkupLine("[dim]Cancelled.[/]");
            return issue;
        }

        var questionId = GenerateQuestionId(questionText);
        var userName = gitConfigService.GetUserName();

        var newQuestion = new Question
        {
            Id = questionId,
            Text = questionText,
            AskedAt = DateTimeOffset.UtcNow,
            AskedBy = userName
        };

        var updatedQuestions = (issue.Questions ?? []).ToList();
        updatedQuestions.Add(newQuestion);

        try
        {
            var updated = await issueService.UpdateQuestionsAsync(issue.Id, updatedQuestions, cancellationToken);
            console.MarkupLine($"[green]Added question[/] [bold]{questionId}[/]");
            return updated;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            console.MarkupLine("[dim]Press any key to continue...[/]");
            console.Input.ReadKey(intercept: true);
            return issue;
        }
    }

    private async Task<Issue> AnswerQuestionAsync(Issue issue, CancellationToken cancellationToken)
    {
        var unanswered = (issue.Questions ?? []).Where(q => q.Answer is null).ToList();

        if (unanswered.Count == 0)
        {
            console.MarkupLine("[dim]No unanswered questions.[/]");
            console.MarkupLine("[dim]Press any key to continue...[/]");
            console.Input.ReadKey(intercept: true);
            return issue;
        }

        var selected = console.Prompt(
            new SelectionPrompt<Question>()
                .Title("[bold]Select a question to answer:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .UseConverter(q => $"{q.Id}: {Markup.Escape(q.Text)}")
                .AddChoices(unanswered));

        var answerText = console.Prompt(
            new TextPrompt<string>("[bold]Answer:[/]")
                .PromptStyle(new Style(Color.Cyan1))
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(answerText))
        {
            console.MarkupLine("[dim]Cancelled.[/]");
            return issue;
        }

        var userName = gitConfigService.GetUserName();
        var questions = (issue.Questions ?? []).ToList();
        var questionIndex = questions.FindIndex(q => q.Id == selected.Id);

        if (questionIndex < 0)
        {
            console.MarkupLine("[red]Question not found.[/]");
            return issue;
        }

        questions[questionIndex] = questions[questionIndex] with
        {
            Answer = answerText,
            AnsweredAt = DateTimeOffset.UtcNow,
            AnsweredBy = userName
        };

        try
        {
            var updated = await issueService.UpdateQuestionsAsync(issue.Id, questions, cancellationToken);
            console.MarkupLine($"[green]Answered question[/] [bold]{selected.Id}[/]");
            return updated;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            console.MarkupLine("[dim]Press any key to continue...[/]");
            console.Input.ReadKey(intercept: true);
            return issue;
        }
    }

    private void RenderQuestions(Issue issue)
    {
        var questions = issue.Questions ?? [];

        if (questions.Count == 0)
        {
            var emptyPanel = new Panel("[dim]No questions on this issue.[/]")
            {
                Header = new PanelHeader($"[bold]Questions — {issue.Id}[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };
            console.Write(emptyPanel);
            return;
        }

        var lines = new List<string>();
        foreach (var q in questions)
        {
            var answeredStatus = q.Answer is not null ? "[green]\u2713[/]" : "[yellow]?[/]";
            lines.Add($"{answeredStatus} [bold]{q.Id}:[/] {Markup.Escape(q.Text)}");

            if (q.AskedBy is not null)
            {
                lines.Add($"   [dim]Asked by {Markup.Escape(q.AskedBy)} at {q.AskedAt:g}[/]");
            }
            else
            {
                lines.Add($"   [dim]Asked at {q.AskedAt:g}[/]");
            }

            if (q.Answer is not null)
            {
                lines.Add($"   [green]A:[/] {Markup.Escape(q.Answer)}");
                if (q.AnsweredBy is not null)
                {
                    lines.Add($"   [dim]Answered by {Markup.Escape(q.AnsweredBy)} at {q.AnsweredAt:g}[/]");
                }
            }
            else
            {
                lines.Add("   [yellow](Unanswered)[/]");
            }

            lines.Add("");
        }

        var panel = new Panel(string.Join("\n", lines).TrimEnd())
        {
            Header = new PanelHeader($"[bold]Questions — {issue.Id}[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        console.Write(panel);
    }

    private static string GenerateQuestionId(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text + DateTimeOffset.UtcNow.Ticks));
        var hashString = Convert.ToHexString(hash);
        return "Q" + hashString[..5];
    }
}
