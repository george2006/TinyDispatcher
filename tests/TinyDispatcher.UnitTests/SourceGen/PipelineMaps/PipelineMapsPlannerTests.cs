#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
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
            pipelinePlan: null,
            options: Options(emitPipelineMap: false, pipelineMapFormat: "json"));

        Assert.False(plan.ShouldEmit);
        Assert.Empty(plan.Descriptors);
    }

    [Fact]
    public void Build_defaults_to_json_when_format_is_unknown()
    {
        var plan = PipelineMapsPlanner.Build(
            Discovery("global::MyApp.Ping", "global::MyApp.PingHandler"),
            EmptyContributions(),
            pipelinePlan: null,
            options: Options(emitPipelineMap: true, pipelineMapFormat: "bogus"));

        Assert.True(plan.ShouldEmit);
        Assert.True(plan.Formats.EmitJson);
        Assert.False(plan.Formats.EmitMermaid);
        Assert.Single(plan.Descriptors);
    }

    [Fact]
    public void Build_uses_policy_selected_by_executable_pipeline_plan()
    {
        var discovery = Discovery(
            "global::MyApp.Ping",
            "global::MyApp.PingHandler");
        var policies = ImmutableDictionary<string, PolicySpec>.Empty
            .Add(
                "global::MyApp.ZuluPolicy",
                Policy(
                    "global::MyApp.ZuluPolicy",
                    "global::MyApp.ZuluMiddleware"))
            .Add(
                "global::MyApp.AlphaPolicy",
                Policy(
                    "global::MyApp.AlphaPolicy",
                    "global::MyApp.AlphaMiddleware"));
        var contributions = PipelineContributions.Create(
            ImmutableArray<MiddlewareRef>.Empty,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            policies);
        var options = Options(emitPipelineMap: true, pipelineMapFormat: "json");
        var pipelinePlan = PipelinePlanner.Build(
            contributions,
            discovery,
            options);

        var mapPlan = PipelineMapsPlanner.Build(
            discovery,
            contributions,
            pipelinePlan,
            options);

        var descriptor = Assert.Single(mapPlan.Descriptors);

        Assert.Single(descriptor.PoliciesApplied);
        Assert.Equal(
            "global::MyApp.AlphaPolicy",
            descriptor.PoliciesApplied[0]);
        Assert.Single(descriptor.Middlewares);
        Assert.Equal(
            "global::MyApp.AlphaMiddleware",
            descriptor.Middlewares[0].MiddlewareFullName);
    }

    private static DiscoveryResult Discovery(string commandFqn, string handlerFqn)
    {
        return new DiscoveryResult(
            Commands: ImmutableArray.Create(new HandlerContract(commandFqn, handlerFqn, "global::MyApp.AppContext")),
            Queries: ImmutableArray<QueryHandlerContract>.Empty);
    }

    private static PipelineContributions EmptyContributions()
    {
        return PipelineContributions.Create(
            ImmutableArray<MiddlewareRef>.Empty,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
    }

    private static PolicySpec Policy(string policyTypeFqn, string middlewareTypeFqn)
    {
        return new PolicySpec(
            PolicyTypeFqn: policyTypeFqn,
            Middlewares: ImmutableArray.Create(new MiddlewareRef(
                OpenTypeFqn: middlewareTypeFqn,
                Arity: 2)),
            Commands: ImmutableArray.Create("global::MyApp.Ping"));
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

