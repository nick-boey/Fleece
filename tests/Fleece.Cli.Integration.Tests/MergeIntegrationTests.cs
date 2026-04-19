namespace Fleece.Cli.Integration.Tests;

[TestFixture]
[NonParallelizable]
public class MergeIntegrationTests : GitTempRepoFixture
{
    [Test]
    public async Task Merge_consolidates_multiple_unmerged_issue_files()
    {
        (await RunCliAsync("create", "-t", "Issue A", "-y", "task", "-d", "b")).Should().Be(0);

        // Duplicate the file to simulate a git merge that produced two unmerged issue files.
        var fleeceDir = Path.Combine(TempDir, ".fleece");
        var original = Directory.EnumerateFiles(fleeceDir, "issues_*.jsonl").Single();
        File.Copy(original, Path.Combine(fleeceDir, "issues_duplicate.jsonl"));

        var exit = await RunCliAsync("merge");
        exit.Should().Be(0);

        Directory.EnumerateFiles(fleeceDir, "issues_*.jsonl").Should().ContainSingle();
    }
}
