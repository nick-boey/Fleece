using Fleece.Cli.Commands;
using Fleece.Cli.Settings;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Fleece.Cli.Tests.Commands;

[TestFixture]
public class MergeCommandTests
{
    [Test]
    public async Task Execute_WritesDeprecationNoticeToStderr()
    {
        var fleeceService = Substitute.For<IFleeceService>();
        fleeceService.MergeAsync(Arg.Any<bool>()).Returns(0);
        var console = new TestConsole();
        var command = new MergeCommand(fleeceService, console);
        var context = new CommandContext([], Substitute.For<IRemainingArguments>(), "merge", null);

        var sw = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(sw);
        try
        {
            await command.ExecuteAsync(context, new MergeSettings { DryRun = true });
        }
        finally
        {
            Console.SetError(originalErr);
        }

        sw.ToString().Should().Contain("deprecated");
        sw.ToString().Should().Contain("fleece project");
    }
}
