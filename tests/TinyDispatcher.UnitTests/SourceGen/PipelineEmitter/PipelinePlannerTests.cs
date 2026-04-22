#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelinePlannerTests
{
    private static MiddlewareRef Mw(string openTypeFqn, int arity)
        => new MiddlewareRef(OpenTypeFqn: openTypeFqn, Arity: arity);

    [Fact]
    public void Build_global_only_creates_global_pipeline_and_registers_global_for_all_commands()
    {
        var global = ImmutableArray.Create(
            Mw("global::MyApp.GlobalLogMiddleware", 2));

        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;
        var policies = ImmutableDictionary<string, PolicySpec>.Empty;

        var discovery = FakeDiscovery(
            "global::MyApp.CmdA",
            "global::MyApp.CmdB");

        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(Contributions(global, perCommand, policies), discovery, options);

        Assert.True(plan.ShouldEmit);
        Assert.NotNull(plan.GlobalPipeline);
        Assert.True(plan.GlobalPipeline!.IsOpenGeneric);
        Assert.Equal("TinyDispatcherGlobalPipeline", plan.GlobalPipeline.ClassName);
        AssertStepNames(
            plan.GlobalPipeline.Steps,
            "global::MyApp.GlobalLogMiddleware");

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
                Middlewares: ImmutableArray.Create(Mw("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA", "global::MyApp.CmdB");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(Contributions(global, perCommand, policies), discovery, options);

        Assert.True(plan.ShouldEmit);
        Assert.Null(plan.GlobalPipeline);
        Assert.Single(plan.PolicyPipelines);
        AssertStepNames(
            plan.PolicyPipelines[0].Steps,
            "global::MyApp.PolicyLogMiddleware");

        Assert.Contains(plan.ServiceRegistrations, r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdA", StringComparison.Ordinal) &&
            r.ImplementationTypeExpression.Contains("TinyDispatcherPolicyPipeline_", StringComparison.Ordinal) &&
            r.ImplementationTypeExpression.Contains("<global::MyApp.CmdA>", StringComparison.Ordinal));

        Assert.DoesNotContain(plan.ServiceRegistrations, r =>
            r.ServiceTypeExpression.Contains("ICommandPipeline<global::MyApp.CmdB", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_global_and_policy_policy_steps_are_global_then_policy()
    {
        var global = ImmutableArray.Create(Mw("global::MyApp.GlobalLogMiddleware", 2));
        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;

        var policies = ImmutableDictionary<string, PolicySpec>.Empty.Add(
            "global::MyApp.CheckoutPolicy",
            new PolicySpec(
                PolicyTypeFqn: "global::MyApp.CheckoutPolicy",
                Middlewares: ImmutableArray.Create(Mw("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(Contributions(global, perCommand, policies), discovery, options);

        Assert.Single(plan.PolicyPipelines);
        AssertStepNames(
            plan.PolicyPipelines[0].Steps,
            "global::MyApp.GlobalLogMiddleware",
            "global::MyApp.PolicyLogMiddleware");
    }

    [Fact]
    public void Build_global_policy_and_per_command_per_command_steps_are_global_then_policy_then_per_command()
    {
        var global = ImmutableArray.Create(Mw("global::MyApp.GlobalLogMiddleware", 2));

        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
            "global::MyApp.CmdA",
            ImmutableArray.Create(Mw("global::MyApp.PerCommandLogMiddleware", 2)));

        var policies = ImmutableDictionary<string, PolicySpec>.Empty.Add(
            "global::MyApp.CheckoutPolicy",
            new PolicySpec(
                PolicyTypeFqn: "global::MyApp.CheckoutPolicy",
                Middlewares: ImmutableArray.Create(Mw("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(Contributions(global, perCommand, policies), discovery, options);

        Assert.Single(plan.PerCommandPipelines);

        var pc = plan.PerCommandPipelines[0];
        AssertStepNames(
            pc.Steps,
            "global::MyApp.GlobalLogMiddleware",
            "global::MyApp.PolicyLogMiddleware",
            "global::MyApp.PerCommandLogMiddleware");
    }

    [Fact]
    public void Build_service_registrations_preference_is_per_command_over_policy_over_global()
    {
        var global = ImmutableArray.Create(Mw("global::MyApp.GlobalLogMiddleware", 2));

        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
            "global::MyApp.CmdA",
            ImmutableArray.Create(Mw("global::MyApp.PerCommandLogMiddleware", 2)));

        var policies = ImmutableDictionary<string, PolicySpec>.Empty.Add(
            "global::MyApp.CheckoutPolicy",
            new PolicySpec(
                PolicyTypeFqn: "global::MyApp.CheckoutPolicy",
                Middlewares: ImmutableArray.Create(Mw("global::MyApp.PolicyLogMiddleware", 2)),
                Commands: ImmutableArray.Create("global::MyApp.CmdA"))
        );

        var discovery = FakeDiscovery("global::MyApp.CmdA");
        var options = FakeOptions("MyApp.Generated", "global::MyApp.AppContext");

        var plan = PipelinePlanner.Build(Contributions(global, perCommand, policies), discovery, options);

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

    private static PipelineContributions Contributions(
        ImmutableArray<MiddlewareRef> globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        return PipelineContributions.Create(globals, perCommand, policies);
    }

    private static void AssertStepNames(ImmutableArray<MiddlewareStep> steps, params string[] expected)
    {
        Assert.Equal(expected.Length, steps.Length);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], steps[i].Middleware.OpenTypeFqn);
        }
    }
}

