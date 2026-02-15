#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelinePlannerTests
{
    [Fact]
    public void Build_global_only_creates_global_pipeline_and_registers_global_for_all_commands()
    {
        var global = ImmutableArray.Create(
            new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2));

        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;
        var policies = ImmutableDictionary<string, PolicySpec>.Empty;

        var discovery = FakeDiscovery(
            "global::MyApp.CmdA",
            "global::MyApp.CmdB");

        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(global, perCommand, policies, discovery, options);

        Assert.True(plan.ShouldEmit);
        Assert.NotNull(plan.GlobalPipeline);
        Assert.True(plan.GlobalPipeline!.IsOpenGeneric);
        Assert.Equal("TinyDispatcherGlobalPipeline", plan.GlobalPipeline.ClassName);

        Assert.Contains(plan.ServiceRegistrations, r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdA", StringComparison.Ordinal) &&
            r.ImplementationTypeExpression.Contains("TinyDispatcherGlobalPipeline<global::MyApp.CmdA>", StringComparison.Ordinal));

        Assert.Contains(plan.ServiceRegistrations, r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdB", StringComparison.Ordinal) &&
            r.ImplementationTypeExpression.Contains("TinyDispatcherGlobalPipeline<global::MyApp.CmdB>", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_policy_only_creates_policy_pipeline_and_registers_policy_for_commands()
    {
        var global = ImmutableArray<MiddlewareRef>.Empty;
        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;

        var policies = ImmutableDictionary<string, PolicySpec>.Empty.Add(
            "global::MyApp.CheckoutPolicy",
            new PolicySpec(
                PolicyTypeFqn: "global::MyApp.CheckoutPolicy",
                Middlewares: ImmutableArray.Create(new MiddlewareRef("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA", "global::MyApp.CmdB");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(global, perCommand, policies, discovery, options);

        Assert.True(plan.ShouldEmit);
        Assert.Null(plan.GlobalPipeline);
        Assert.Single(plan.PolicyPipelines);

        Assert.Contains(plan.ServiceRegistrations, r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdA", StringComparison.Ordinal) &&
            r.ImplementationTypeExpression.Contains("TinyDispatcherPolicyPipeline_", StringComparison.Ordinal) &&
            r.ImplementationTypeExpression.Contains("<global::MyApp.CmdA>", StringComparison.Ordinal));

        Assert.DoesNotContain(plan.ServiceRegistrations, r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdB", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_global_policy_and_per_command_per_command_steps_are_global_then_policy_then_per_command()
    {
        var global = ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2));

        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
            "global::MyApp.CmdA",
            ImmutableArray.Create(new MiddlewareRef("global::MyApp.PerCommandLogMiddleware", 2)));

        var policies = ImmutableDictionary<string, PolicySpec>.Empty.Add(
            "global::MyApp.CheckoutPolicy",
            new PolicySpec(
                PolicyTypeFqn: "global::MyApp.CheckoutPolicy",
                Middlewares: ImmutableArray.Create(new MiddlewareRef("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(global, perCommand, policies, discovery, options);

        Assert.Single(plan.PerCommandPipelines);

        var pc = plan.PerCommandPipelines[0];
        var steps = pc.Steps.Select(s => s.Middleware.OpenTypeFqn).ToArray();

        Assert.Equal(
            new[]
            {
                "global::MyApp.GlobalLogMiddleware",
                "global::MyApp.PolicyLogMiddleware",
                "global::MyApp.PerCommandLogMiddleware"
            },
            steps);
    }

    [Fact]
    public void Build_service_registrations_preference_is_per_command_over_policy_over_global()
    {
        var global = ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2));

        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
            "global::MyApp.CmdA",
            ImmutableArray.Create(new MiddlewareRef("global::MyApp.PerCommandLogMiddleware", 2)));

        var policies = ImmutableDictionary<string, PolicySpec>.Empty.Add(
            "global::MyApp.CheckoutPolicy",
            new PolicySpec(
                PolicyTypeFqn: "global::MyApp.CheckoutPolicy",
                Middlewares: ImmutableArray.Create(new MiddlewareRef("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(global, perCommand, policies, discovery, options);

        var reg = plan.ServiceRegistrations.Single(r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdA", StringComparison.Ordinal));

        Assert.Contains("TinyDispatcherPipeline_", reg.ImplementationTypeExpression, StringComparison.Ordinal);
        Assert.DoesNotContain("TinyDispatcherPolicyPipeline_", reg.ImplementationTypeExpression, StringComparison.Ordinal);
        Assert.DoesNotContain("TinyDispatcherGlobalPipeline<", reg.ImplementationTypeExpression, StringComparison.Ordinal);
    }

    private static GeneratorOptions FakeOptions(string genNs, string ctxFqn)
        => new(
            GeneratedNamespace: genNs,
            EmitDiExtensions: false,
            EmitHandlerRegistrations: false,
            IncludeNamespacePrefix: null,
            CommandContextType: ctxFqn,
            EmitPipelineMap: false,
            PipelineMapFormat: null);

    private static DiscoveryResult FakeDiscovery(params string[] commandMessageTypeFqns)
        => new(
            Commands: commandMessageTypeFqns
                .Select(fqn => new HandlerContract(MessageTypeFqn: fqn, HandlerTypeFqn: "global::MyApp.DummyHandler"))
                .ToImmutableArray(),
            Queries: ImmutableArray<QueryHandlerContract>.Empty);
}
