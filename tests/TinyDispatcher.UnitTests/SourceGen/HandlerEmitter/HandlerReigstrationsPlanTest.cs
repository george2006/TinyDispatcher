#nullable enable

using TinyDispatcher.SourceGen.Emitters.Handlers;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen.HandlerEmitter;

public sealed class HandlerRegistrationsPlanTests
{
    [Fact]
    public void Disabled_creates_empty_disabled_plan()
    {
        var plan = HandlerRegistrationsPlan.Disabled("Acme.Gen");

        Assert.Equal("Acme.Gen", plan.Namespace);
        Assert.False(plan.IsEnabled);
        Assert.Equal(string.Empty, plan.CommandContextFqn);
        Assert.Empty(plan.Commands);
        Assert.Empty(plan.Queries);
        Assert.False(plan.ShouldInsertBlankLineBetweenCommandAndQueryBlocks);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    public void ShouldInsertBlankLineBetweenCommandAndQueryBlocks_depends_on_both_blocks_present(
        int commandCount,
        int queryCount,
        bool expected)
    {
        var commands = commandCount == 0
            ? System.Array.Empty<HandlerContract>()
            : new[] { new HandlerContract("global::A.Cmd", "global::A.CmdHandler") };

        var queries = queryCount == 0
            ? System.Array.Empty<QueryHandlerContract>()
            : new[] { new QueryHandlerContract("global::A.Q", "global::A.R", "global::A.QHandler") };

        var plan = new HandlerRegistrationsPlan(
            @namespace: "Acme.Gen",
            isEnabled: true,
            commandContextFqn: "global::Acme.Ctx",
            commands: commands,
            queries: queries);

        Assert.Equal(expected, plan.ShouldInsertBlankLineBetweenCommandAndQueryBlocks);
    }
}