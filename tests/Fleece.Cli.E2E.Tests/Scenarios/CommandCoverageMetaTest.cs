using System.Reflection;

namespace Fleece.Cli.E2E.Tests.Scenarios;

[TestFixture]
public class CommandCoverageMetaTest
{
    [Test]
    public void Every_command_in_CliComposition_has_at_least_one_scenario()
    {
        var assembly = typeof(CliScenarioTestBase).Assembly;

        var categories = assembly.GetTypes()
            .Where(t => typeof(CliScenarioTestBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetCustomAttributes<CategoryAttribute>().Select(c => c.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = CliComposition.Commands
            .Where(c => !categories.Contains(c.Name))
            .Select(c => c.Name)
            .ToList();

        uncovered.Should().BeEmpty(
            "every CliComposition.Commands entry must appear in at least one " +
            "[Category(...)] attribute on a scenario fixture. Uncovered: {0}",
            string.Join(", ", uncovered));
    }
}
