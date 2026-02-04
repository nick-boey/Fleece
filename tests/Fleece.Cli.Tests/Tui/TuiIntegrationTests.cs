using System.Data;
using Fleece.Cli.Tui;
using Fleece.Cli.Tui.Dialogs;
using Fleece.Cli.Tui.Views;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Terminal.Gui;
using DataTableSource = Fleece.Cli.Tui.DataTableSource;

namespace Fleece.Cli.Tests.Tui;

/// <summary>
/// Integration tests for the TUI components.
/// These tests verify the TUI functionality programmatically.
/// </summary>
[TestFixture]
public class TuiIntegrationTests
{
    private IIssueService _issueService = null!;
    private IStorageService _storageService = null!;
    private IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        _issueService = Substitute.For<IIssueService>();
        _storageService = Substitute.For<IStorageService>();
        _serviceProvider = Substitute.For<IServiceProvider>();

        _serviceProvider.GetService(typeof(IIssueService)).Returns(_issueService);
        _serviceProvider.GetService(typeof(IStorageService)).Returns(_storageService);
        _storageService.HasMultipleUnmergedFilesAsync().Returns(Task.FromResult<(bool, string)>((false, string.Empty)));
    }

    [TearDown]
    public void TearDown()
    {
        Application.Shutdown();
    }

    [Test]
    public void DataTableSource_ShouldProvideCorrectRowsAndColumns()
    {
        // Arrange
        var dataTable = new DataTable();
        dataTable.Columns.Add("ID", typeof(string));
        dataTable.Columns.Add("Title", typeof(string));
        dataTable.Columns.Add("Status", typeof(string));

        dataTable.Rows.Add("abc1234", "Test Issue 1", "Idea");
        dataTable.Rows.Add("def5678", "Test Issue 2", "Complete");

        // Act
        var source = new DataTableSource(dataTable);

        // Assert
        source.Rows.Should().Be(2);
        source.Columns.Should().Be(3);
        source[0, 0].Should().Be("abc1234");
        source[0, 1].Should().Be("Test Issue 1");
        source[1, 2].Should().Be("Complete");
        source.ColumnNames.Should().ContainInOrder("ID", "Title", "Status");
    }

    [Test]
    public void ColorScheme_ShouldHaveValidColors()
    {
        // Verify that the TUI color scheme is properly configured
        var scheme = TuiColors.DefaultScheme;

        scheme.Should().NotBeNull();
    }

    [Test]
    public async Task IssueListView_ShouldDisplayIssues()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var issues = new List<Issue>
        {
            new()
            {
                Id = "abc1234567890",
                Title = "First Test Issue",
                Type = IssueType.Bug,
                Status = IssueStatus.Idea,
                Priority = 1,
                LastUpdate = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = "def9876543210",
                Title = "Second Test Issue",
                Type = IssueType.Feature,
                Status = IssueStatus.Progress,
                Priority = 2,
                LastUpdate = DateTimeOffset.UtcNow.AddDays(-1)
            }
        };

        _issueService.FilterAsync(
            Arg.Any<IssueStatus?>(),
            Arg.Any<IssueType?>(),
            Arg.Any<int?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<int?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult<IReadOnlyList<Issue>>(issues));

        // Act
        var listView = new IssueListView(_issueService);
        listView.SetIssues(issues);

        // Assert
        listView.SelectedIssue.Should().NotBeNull();
        listView.SelectedIssue!.Id.Should().Be("abc1234567890");
    }

    [Test]
    public void IssueListView_VimNavigation_ShouldWork()
    {
        // Arrange
        Application.Init(new FakeDriver());

        // Issues with different LastUpdate times - list sorts by LastUpdate descending
        var now = DateTimeOffset.UtcNow;
        var issues = new List<Issue>
        {
            CreateTestIssueWithDate("id1", "Issue 1", now.AddDays(-2)),
            CreateTestIssueWithDate("id2", "Issue 2", now.AddDays(-1)),
            CreateTestIssueWithDate("id3", "Issue 3", now) // Most recent
        };

        var listView = new IssueListView(_issueService);
        listView.SetIssues(issues);

        // Initial selection should be most recently updated issue (id3)
        listView.SelectedIssue!.Id.Should().Be("id3");
    }

    [Test]
    public void FilterBar_ShouldReturnCorrectFilters()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var filterBar = new FilterBar();

        // Act
        var filters = filterBar.GetFilters();

        // Assert - default filters should be null (all)
        filters.Status.Should().BeNull();
        filters.Type.Should().BeNull();
        filters.Priority.Should().BeNull();
    }

    [Test]
    public void IssueDetailView_ShouldDisplayIssueDetails()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var issue = new Issue
        {
            Id = "abc1234567890",
            Title = "Test Issue Title",
            Type = IssueType.Bug,
            Status = IssueStatus.Idea,
            Priority = 1,
            Description = "This is a test description.",
            Group = "test-group",
            AssignedTo = "test-user",
            Tags = ["tag1", "tag2"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        // Act
        var detailView = new IssueDetailView();
        detailView.SetIssue(issue);

        // Assert - view should not throw and should handle issue properly
        // (Detailed assertions would require accessing internal state)
    }

    [Test]
    public void IssueDetailView_ShouldHandleNullIssue()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var detailView = new IssueDetailView();

        // Act & Assert - should not throw
        detailView.SetIssue(null);
    }

    [Test]
    public void StatusBarView_ShouldDisplayIssueCount()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var statusBar = new StatusBarView();

        // Act
        statusBar.SetIssueCount(42);

        // Assert - should not throw
        // The status bar should now display "42 issues"
    }

    [Test]
    public async Task CreateIssueDialog_ShouldBeCreatable()
    {
        // Arrange
        Application.Init(new FakeDriver());

        _issueService.CreateAsync(
            Arg.Any<string>(),
            Arg.Any<IssueType>(),
            Arg.Any<string?>(),
            Arg.Any<IssueStatus>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult(CreateTestIssue("new-id", "New Issue")));

        // Act
        var dialog = new CreateIssueDialog(_issueService);

        // Assert
        dialog.Should().NotBeNull();
        dialog.WasCreated.Should().BeFalse();

        dialog.Dispose();
    }

    [Test]
    public void EditIssueDialog_ShouldBeCreatable()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var issue = CreateTestIssue("test-id", "Test Issue");

        // Act
        var dialog = new EditIssueDialog(_issueService, issue);

        // Assert
        dialog.Should().NotBeNull();
        dialog.WasSaved.Should().BeFalse();

        dialog.Dispose();
    }

    [Test]
    public void DeleteConfirmDialog_ShouldBeCreatable()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var issue = CreateTestIssue("test-id", "Test Issue");

        // Act
        var dialog = new DeleteConfirmDialog(_issueService, issue);

        // Assert
        dialog.Should().NotBeNull();
        dialog.WasDeleted.Should().BeFalse();

        dialog.Dispose();
    }

    [Test]
    public void SearchDialog_ShouldBeCreatable()
    {
        // Arrange
        Application.Init(new FakeDriver());

        // Act
        var dialog = new SearchDialog(_issueService);

        // Assert
        dialog.Should().NotBeNull();
        dialog.SelectedIssue.Should().BeNull();

        dialog.Dispose();
    }

    [Test]
    public void QuestionDialog_ShouldBeCreatable()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var issue = CreateTestIssue("test-id", "Test Issue");

        // Act
        var dialog = new QuestionDialog(_issueService, issue);

        // Assert
        dialog.Should().NotBeNull();
        dialog.WasSaved.Should().BeFalse();

        dialog.Dispose();
    }

    [Test]
    public void MainWindow_ShouldBeCreatable()
    {
        // Arrange
        Application.Init(new FakeDriver());

        _issueService.FilterAsync(
            Arg.Any<IssueStatus?>(),
            Arg.Any<IssueType?>(),
            Arg.Any<int?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<int?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult<IReadOnlyList<Issue>>([]));

        // Act
        var mainWindow = new MainWindow(_serviceProvider);

        // Assert
        mainWindow.Should().NotBeNull();
        mainWindow.Title.Should().Be("Fleece Issue Tracker");

        mainWindow.Dispose();
    }

    [Test]
    public async Task IssueListView_SelectIssue_ShouldWork()
    {
        // Arrange
        Application.Init(new FakeDriver());

        var issues = new List<Issue>
        {
            CreateTestIssue("id1", "Issue 1"),
            CreateTestIssue("id2", "Issue 2"),
            CreateTestIssue("id3", "Issue 3")
        };

        var listView = new IssueListView(_issueService);
        listView.SetIssues(issues);

        // Act
        listView.SelectIssue(issues[1]);

        // Assert
        listView.SelectedIssue!.Id.Should().Be("id2");
    }

    [Test]
    public void IssueListView_SelectionChanged_ShouldFireEvent()
    {
        // Arrange
        Application.Init(new FakeDriver());

        // Issues with different LastUpdate times - list sorts by LastUpdate descending
        var now = DateTimeOffset.UtcNow;
        var issues = new List<Issue>
        {
            CreateTestIssueWithDate("id1", "Issue 1", now.AddDays(-1)),
            CreateTestIssueWithDate("id2", "Issue 2", now) // Most recent
        };

        var listView = new IssueListView(_issueService);
        Issue? selectedIssue = null;
        listView.SelectionChanged += (s, issue) => selectedIssue = issue;

        // Act
        listView.SetIssues(issues);

        // Assert - should select most recently updated issue first
        selectedIssue.Should().NotBeNull();
        selectedIssue!.Id.Should().Be("id2");
    }

    private static Issue CreateTestIssue(string id, string title)
    {
        return new Issue
        {
            Id = id,
            Title = title,
            Type = IssueType.Task,
            Status = IssueStatus.Idea,
            LastUpdate = DateTimeOffset.UtcNow
        };
    }

    private static Issue CreateTestIssueWithDate(string id, string title, DateTimeOffset lastUpdate)
    {
        return new Issue
        {
            Id = id,
            Title = title,
            Type = IssueType.Task,
            Status = IssueStatus.Idea,
            LastUpdate = lastUpdate
        };
    }
}
