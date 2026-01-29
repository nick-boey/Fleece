using System.Collections.ObjectModel;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Terminal.Gui;

namespace Fleece.Cli.Tui.Dialogs;

/// <summary>
/// Dialog for managing questions on an issue.
/// </summary>
public sealed class QuestionDialog : Dialog
{
    private readonly IIssueService _issueService;
    private readonly Issue _issue;
    private readonly ListView _questionsList;
    private readonly TextField _questionField;
    private readonly TextField _answerField;
    private IReadOnlyList<Question> _questions;
    private ObservableCollection<string> _questionsSource;

    /// <summary>
    /// Gets whether changes were saved.
    /// </summary>
    public bool WasSaved { get; private set; }

    public QuestionDialog(IIssueService issueService, Issue issue)
    {
        _issueService = issueService;
        _issue = issue;
        _questions = issue.Questions;
        _questionsSource = new ObservableCollection<string>();

        var shortId = issue.Id.Length > 7 ? issue.Id[..7] : issue.Id;
        Title = $"Questions - {shortId}";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        // Existing questions list
        var listLabel = new Label { X = 1, Y = 0, Text = "Existing Questions:" };

        var listFrame = new FrameView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Percent(50),
            BorderStyle = LineStyle.Single
        };

        _questionsList = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new ListWrapper<string>(_questionsSource)
        };
        UpdateQuestionsList();
        listFrame.Add(_questionsList);

        // Answer section
        var answerLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(listFrame) + 1,
            Text = "Answer selected question:"
        };

        _answerField = new TextField
        {
            X = 1,
            Y = Pos.Bottom(listFrame) + 2,
            Width = Dim.Fill(12)
        };

        var answerButton = new Button
        {
            X = Pos.Right(_answerField) + 1,
            Y = Pos.Bottom(listFrame) + 2,
            Text = "Answer"
        };
        answerButton.Accepting += OnAnswer;

        // New question section
        var newLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(listFrame) + 4,
            Text = "Ask new question:"
        };

        _questionField = new TextField
        {
            X = 1,
            Y = Pos.Bottom(listFrame) + 5,
            Width = Dim.Fill(12)
        };

        var askButton = new Button
        {
            X = Pos.Right(_questionField) + 1,
            Y = Pos.Bottom(listFrame) + 5,
            Text = "Ask"
        };
        askButton.Accepting += OnAsk;

        // Handle Enter key in question field
        _questionField.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                AskQuestion();
                e.Handled = true;
            }
        };

        // Buttons
        var closeButton = new Button
        {
            Text = "Close",
            IsDefault = true
        };
        closeButton.Accepting += (s, e) => RequestStop();

        Add(listLabel, listFrame, answerLabel, _answerField, answerButton, newLabel, _questionField, askButton);
        AddButton(closeButton);

        _questionField.SetFocus();
    }

    private void UpdateQuestionsList()
    {
        _questionsSource.Clear();

        if (_questions.Count == 0)
        {
            _questionsSource.Add("(No questions yet)");
        }
        else
        {
            foreach (var q in _questions)
            {
                var status = string.IsNullOrEmpty(q.Answer) ? "[?]" : "[A]";
                var preview = q.Text.Length > 60 ? q.Text[..57] + "..." : q.Text;
                _questionsSource.Add($"{status} {q.Id}: {preview}");
            }
        }

        _questionsList.Source = new ListWrapper<string>(_questionsSource);
    }

    private void OnAsk(object? sender, CommandEventArgs e)
    {
        AskQuestion();
    }

    private void AskQuestion()
    {
        var questionText = _questionField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(questionText))
        {
            MessageBox.ErrorQuery("Validation Error", "Please enter a question.", "OK");
            return;
        }

        try
        {
            // Create new question
            var newQuestion = new Question
            {
                Id = $"Q{Random.Shared.Next(10000, 99999):X5}",
                Text = questionText,
                AskedAt = DateTimeOffset.UtcNow
            };

            var updatedQuestions = _questions.Append(newQuestion).ToList();

            var updatedIssue = _issueService.UpdateQuestionsAsync(_issue.Id, updatedQuestions).GetAwaiter().GetResult();
            _questions = updatedIssue.Questions;

            UpdateQuestionsList();
            _questionField.Text = "";
            WasSaved = true;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to add question: {ex.Message}", "OK");
        }
    }

    private void OnAnswer(object? sender, CommandEventArgs e)
    {
        AnswerQuestion();
    }

    private void AnswerQuestion()
    {
        var selectedIndex = _questionsList.SelectedItem;

        if (selectedIndex < 0 || selectedIndex >= _questions.Count)
        {
            MessageBox.ErrorQuery("Error", "Please select a question to answer.", "OK");
            return;
        }

        var answerText = _answerField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(answerText))
        {
            MessageBox.ErrorQuery("Validation Error", "Please enter an answer.", "OK");
            return;
        }

        try
        {
            var questionToAnswer = _questions[selectedIndex];

            // Create updated question with answer
            var answeredQuestion = questionToAnswer with
            {
                Answer = answerText,
                AnsweredAt = DateTimeOffset.UtcNow
            };

            var updatedQuestions = _questions.Select((q, i) => i == selectedIndex ? answeredQuestion : q).ToList();

            var updatedIssue = _issueService.UpdateQuestionsAsync(_issue.Id, updatedQuestions).GetAwaiter().GetResult();
            _questions = updatedIssue.Questions;

            UpdateQuestionsList();
            _answerField.Text = "";
            WasSaved = true;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to answer question: {ex.Message}", "OK");
        }
    }
}
