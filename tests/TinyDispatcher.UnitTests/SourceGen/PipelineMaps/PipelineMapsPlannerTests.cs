#nullable enable

using System.Collections.Immutable;
using System.Linq;
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
        Assert.Empty(plan.AttributeDescriptors);
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
        var options = Options(emitPipelineMap: true, pipelineMapFormat: "json;attributes");
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

        var attribute = Assert.Single(mapPlan.AttributeDescriptors);
        Assert.Equal("global::MyApp.AlphaPolicy", attribute.PolicyFullName);
        Assert.Equal(0, attribute.GlobalMiddlewareCount);
        Assert.Equal(1, attribute.PolicyMiddlewareCount);
    }

    [Fact]
    public void Build_groups_commands_that_use_the_same_generated_pipeline()
    {
        var discovery = new DiscoveryResult(
            Commands: ImmutableArray.Create(
                new HandlerContract(
                    "global::MyApp.SecondCommand",
                    "global::MyApp.SecondHandler",
                    "global::MyApp.AppContext"),
                new HandlerContract(
                    "global::MyApp.FirstCommand",
                    "global::MyApp.FirstHandler",
                    "global::MyApp.AppContext")),
            Queries: ImmutableArray<QueryHandlerContract>.Empty);
        var contributions = PipelineContributions.Create(
            ImmutableArray.Create(new MiddlewareRef(
                "global::MyApp.GlobalMiddleware",
                2)),
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
        var options = Options(emitPipelineMap: true, pipelineMapFormat: "attributes");
        var pipelinePlan = PipelinePlanner.Build(contributions, discovery, options);

        var mapPlan = PipelineMapsPlanner.Build(
            discovery,
            contributions,
            pipelinePlan,
            options);

        var attribute = Assert.Single(mapPlan.AttributeDescriptors);

        Assert.Equal(
            "global::MyApp.Generated.TinyDispatcherGlobalPipeline_MyApp_AppContext<>",
            attribute.PipelineTypeExpression);
        Assert.Equal("global::MyApp.AppContext", attribute.ContextFullName);
        Assert.Equal(
            new[]
            {
                "global::MyApp.FirstCommand",
                "global::MyApp.SecondCommand"
            },
            attribute.CommandFullNames);
        Assert.Single(attribute.Middlewares);
        Assert.Equal(
            "global::MyApp.GlobalMiddleware",
            attribute.Middlewares[0].OpenTypeFqn);
        Assert.Equal(1, attribute.GlobalMiddlewareCount);
        Assert.Null(attribute.PolicyFullName);
        Assert.Equal(0, attribute.PolicyMiddlewareCount);
    }

    [Fact]
    public void Build_preserves_global_policy_and_operation_boundaries()
    {
        var discovery = Discovery(
            "global::MyApp.Ping",
            "global::MyApp.PingHandler");
        var contributions = PipelineContributions.Create(
            ImmutableArray.Create(new MiddlewareRef(
                "global::MyApp.GlobalMiddleware",
                2)),
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                "global::MyApp.Ping",
                ImmutableArray.Create(new MiddlewareRef(
                    "global::MyApp.OperationMiddleware",
                    2))),
            ImmutableDictionary<string, PolicySpec>.Empty.Add(
                "global::MyApp.PingPolicy",
                Policy(
                    "global::MyApp.PingPolicy",
                    "global::MyApp.PolicyMiddleware")));
        var options = Options(emitPipelineMap: true, pipelineMapFormat: "attributes");
        var pipelinePlan = PipelinePlanner.Build(contributions, discovery, options);

        var mapPlan = PipelineMapsPlanner.Build(
            discovery,
            contributions,
            pipelinePlan,
            options);

        var attribute = Assert.Single(mapPlan.AttributeDescriptors);

        Assert.Equal(1, attribute.GlobalMiddlewareCount);
        Assert.Equal("global::MyApp.PingPolicy", attribute.PolicyFullName);
        Assert.Equal(1, attribute.PolicyMiddlewareCount);
        Assert.Equal(
            new[]
            {
                "global::MyApp.GlobalMiddleware",
                "global::MyApp.PolicyMiddleware",
                "global::MyApp.OperationMiddleware"
            },
            attribute.Middlewares.Select(middleware => middleware.OpenTypeFqn));
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

