#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Emitters.Handlers;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen.HandlerEmitter;

public sealed class HandlerRegistrationsPlannerTests
{
    [Fact]
    public void Build_disables_when_EmitHandlerRegistrations_is_false()
    {
        var result = new DiscoveryResult(
            Commands: ImmutableArray.Create(new HandlerContract("global::A.Cmd", "global::A.CmdHandler")),
            Queries: ImmutableArray.Create(new QueryHandlerContract("global::A.Q", "global::A.R", "global::A.QHandler")));

        var options = new GeneratorOptions(
            GeneratedNamespace: "Acme.Gen",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: false,
            IncludeNamespacePrefix: null,
            CommandContextType: "global::Acme.Ctx",
            EmitPipelineMap: false,
            PipelineMapFormat: null);

        var plan = HandlerRegistrationsPlanner.Build(result, options);

        Assert.False(plan.IsEnabled);
        Assert.Equal("Acme.Gen", plan.Namespace);
        Assert.Empty(plan.Commands);
        Assert.Empty(plan.Queries);
        Assert.Equal(string.Empty, plan.CommandContextFqn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_disables_when_CommandContextType_is_missing(string? ctx)
    {
        var result = new DiscoveryResult(
            Commands: ImmutableArray.Create(new HandlerContract("global::A.Cmd", "global::A.CmdHandler")),
            Queries: ImmutableArray<QueryHandlerContract>.Empty);

        var options = new GeneratorOptions(
            GeneratedNamespace: "Acme.Gen",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: null,
            CommandContextType: ctx,
            EmitPipelineMap: false,
            PipelineMapFormat: null);

        var plan = HandlerRegistrationsPlanner.Build(result, options);

        Assert.False(plan.IsEnabled);
        Assert.Equal("Acme.Gen", plan.Namespace);
        Assert.Empty(plan.Commands);
        Assert.Empty(plan.Queries);
    }

    [Fact]
    public void Build_enables_and_sorts_commands_and_queries()
    {
        var result = new DiscoveryResult(
            Commands: ImmutableArray.Create(
                new HandlerContract("global::Z.Cmd", "global::Z.CmdHandler"),
                new HandlerContract("global::A.Cmd", "global::A.CmdHandler2"),
                new HandlerContract("global::A.Cmd", "global::A.CmdHandler1")),
            Queries: ImmutableArray.Create(
                new QueryHandlerContract("global::Z.Q", "global::Z.R", "global::Z.QH"),
                new QueryHandlerContract("global::A.Q", "global::B.R", "global::A.QH2"),
                new QueryHandlerContract("global::A.Q", "global::A.R", "global::A.QH1")));

        var options = new GeneratorOptions(
            GeneratedNamespace: "Acme.Gen",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: null,
            CommandContextType: "Acme.Ctx", // not global:: on purpose
            EmitPipelineMap: false,
            PipelineMapFormat: null);

        var plan = HandlerRegistrationsPlanner.Build(result, options);

        Assert.True(plan.IsEnabled);
        Assert.Equal("Acme.Gen", plan.Namespace);
        Assert.Equal("global::Acme.Ctx", plan.CommandContextFqn);

        // Commands sorted by MessageTypeFqn then HandlerTypeFqn
        Assert.Equal("global::A.Cmd", plan.Commands[0].MessageTypeFqn);
        Assert.Equal("global::A.CmdHandler1", plan.Commands[0].HandlerTypeFqn);
        Assert.Equal("global::A.Cmd", plan.Commands[1].MessageTypeFqn);
        Assert.Equal("global::A.CmdHandler2", plan.Commands[1].HandlerTypeFqn);
        Assert.Equal("global::Z.Cmd", plan.Commands[2].MessageTypeFqn);

        // Queries sorted by QueryTypeFqn then ResultTypeFqn then HandlerTypeFqn
        Assert.Equal("global::A.Q", plan.Queries[0].QueryTypeFqn);
        Assert.Equal("global::A.R", plan.Queries[0].ResultTypeFqn);
        Assert.Equal("global::A.Q", plan.Queries[1].QueryTypeFqn);
        Assert.Equal("global::B.R", plan.Queries[1].ResultTypeFqn);
        Assert.Equal("global::Z.Q", plan.Queries[2].QueryTypeFqn);
    }
}