#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineMaps;

public sealed class PipelineMapsPlannerTests
{
    [Fact]
    public void Build_returns_empty_plan_when_pipeline_maps_are_disabled()
    {
        var plan = PipelineMapsPlanner.Build(
            Discovery("global::MyApp.Ping", "global::MyApp.PingHandler"),
            EmptyContributions(),
            Options(emitPipelineMap: false, pipelineMapFormat: "json"));

        Assert.False(plan.ShouldEmit);
        Assert.Empty(plan.Descriptors);
    }

    [Fact]
    public void Build_defaults_to_json_when_format_is_unknown()
    {
        var plan = PipelineMapsPlanner.Build(
            Discovery("global::MyApp.Ping", "global::MyApp.PingHandler"),
            EmptyContributions(),
            Options(emitPipelineMap: true, pipelineMapFormat: "bogus"));

        Assert.True(plan.ShouldEmit);
        Assert.True(plan.Formats.EmitJson);
        Assert.False(plan.Formats.EmitMermaid);
        Assert.Single(plan.Descriptors);
    }

    private static DiscoveryResult Discovery(string commandFqn, string handlerFqn)
    {
        return new DiscoveryResult(
            Commands: ImmutableArray.Create(new HandlerContract(commandFqn, handlerFqn)),
            Queries: ImmutableArray<QueryHandlerContract>.Empty);
    }

    private static PipelineContributions EmptyContributions()
    {
        return PipelineContributions.Create(
            ImmutableArray<MiddlewareRef>.Empty,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
    }

    private static GeneratorOptions Options(bool emitPipelineMap, string? pipelineMapFormat)
    {
        return new GeneratorOptions(
            GeneratedNamespace: "MyApp.Generated",
            EmitDiExtensions: false,
            EmitHandlerRegistrations: false,
            IncludeNamespacePrefix: null,
            CommandContextType: "global::MyApp.AppContext",
            EmitPipelineMap: emitPipelineMap,
            PipelineMapFormat: pipelineMapFormat);
    }
}
