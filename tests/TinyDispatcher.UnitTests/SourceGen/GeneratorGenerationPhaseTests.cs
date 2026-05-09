#nullable enable

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorGenerationPhaseTests
{
    [Fact]
    public void Generate_does_not_emit_pipeline_source_for_non_host_project()
    {
        var context = new CapturingGeneratorContext();
        var compilation = CreateCompilation();
        var discovery = EmptyDiscovery();
        var pipeline = new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty.Add(
                    "global::MyApp.Policy",
                    new PolicySpec(
                        "global::MyApp.Policy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray<string>.Empty)));
        var extraction = Extraction(discovery, pipeline);

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: false,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray<UseTinyDispatcherCall>.Empty));

        new GeneratorGenerationPhase().Generate(
            context,
            analysis.EffectiveOptions,
            extraction,
            analysis.HostBootstrap);

        Assert.DoesNotContain(
            context.Sources,
            source => IsPipelineSource(source.HintName));
    }

    [Fact]
    public void Generate_uses_validated_expected_context_for_handler_registrations()
    {
        var context = new CapturingGeneratorContext();
        var compilation = CreateCompilation();
        var command = new HandlerContract(
            MessageTypeFqn: "global::MyApp.CreateUser",
            HandlerTypeFqn: "global::MyApp.CreateUserHandler",
            ContextTypeFqn: "global::MyApp.AppContext");
        var discovery = new DiscoveryResult(
            ImmutableArray.Create(command),
            ImmutableArray<QueryHandlerContract>.Empty);
        var pipeline = new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(discovery, pipeline);
        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: null),
            extraction,
            hostBootstrap);

        var registrations = Assert.Single(
            context.Sources,
            source => source.HintName == "ThisAssemblyHandlerRegistrations.g.cs");

        Assert.Contains(
            "global::TinyDispatcher.ICommandHandler<global::MyApp.CreateUser, global::MyApp.AppContext>",
            registrations.Content);
    }

    [Fact]
    public void Generate_emits_global_pipeline_registrations_for_referenced_contributed_commands()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var pipeline = new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(
            discovery,
            pipeline,
            Referenced(
                ImmutableArray.Create(ReferencedHandler(
                    "ExternalApp",
                    "global::MyApp.AppContext",
                    "global::ExternalApp.CreateOrder",
                    "global::ExternalApp.CreateOrderHandler",
                    "global::MyApp.AppContext")),
                new ReferencedAssemblyContribution(
                    "ExternalApp",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    ImmutableArray<PolicyFinding>.Empty)));

        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            hostBootstrap);

        var pipelineSource = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_AppContext.g.cs");

        Assert.Contains(
            "global::TinyDispatcher.ICommandPipeline<global::ExternalApp.CreateOrder, global::MyApp.AppContext>",
            pipelineSource.Content);
        Assert.Contains(
            "TinyDispatcherGlobalPipeline_MyApp_AppContext<global::ExternalApp.CreateOrder>",
            pipelineSource.Content);
    }

    [Fact]
    public void Generate_merges_referenced_per_command_and_policy_contributions_into_pipeline_emission()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var pipeline = new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(
            discovery,
            pipeline,
            Referenced(
                ImmutableArray.Create(ReferencedHandler(
                    "ExternalApp",
                    "global::MyApp.AppContext",
                    "global::ExternalApp.CreateOrder",
                    "global::ExternalApp.CreateOrderHandler",
                    "global::MyApp.AppContext")),
                new ReferencedAssemblyContribution(
                    "ExternalApp",
                    "global::MyApp.AppContext",
                    ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.GlobalMiddleware", 2)),
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::ExternalApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.OrderMiddleware", 2)))),
                    ImmutableArray.Create(new PolicyFinding(
                        "global::ExternalApp.OrderPolicy",
                        ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.PolicyMiddleware", 2)),
                        ImmutableArray.Create("global::ExternalApp.CreateOrder"))))));

        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            hostBootstrap);

        var pipelineSource = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_AppContext.g.cs");

        Assert.Contains(
            "internal sealed class TinyDispatcherPipeline_CreateOrder",
            pipelineSource.Content);
        Assert.Contains(
            "global::ExternalApp.PolicyMiddleware",
            pipelineSource.Content);
        Assert.Contains(
            "global::ExternalApp.OrderMiddleware",
            pipelineSource.Content);
        Assert.Contains(
            "global::ExternalApp.GlobalMiddleware",
            pipelineSource.Content);
    }

    [Fact]
    public void Generate_ignores_referenced_contributions_from_mismatched_context_assemblies()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var pipeline = new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(
            discovery,
            pipeline,
            Referenced(
                ImmutableArray.Create(
                    ReferencedHandler(
                        "Matching",
                        "global::MyApp.AppContext",
                        "global::ExternalApp.CreateOrder",
                        "global::ExternalApp.CreateOrderHandler",
                        "global::MyApp.AppContext"),
                    ReferencedHandler(
                        "Mismatched",
                        "global::OtherApp.OtherContext",
                        "global::OtherApp.CancelOrder",
                        "global::OtherApp.CancelOrderHandler",
                        "global::OtherApp.OtherContext")),
                new ReferencedAssemblyContribution(
                    "Matching",
                    "global::MyApp.AppContext",
                    ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.GlobalMiddleware", 2)),
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::ExternalApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.OrderMiddleware", 2)))),
                    ImmutableArray<PolicyFinding>.Empty),
                new ReferencedAssemblyContribution(
                    "Mismatched",
                    "global::OtherApp.OtherContext",
                    ImmutableArray.Create(new MiddlewareRef("global::OtherApp.GlobalMiddleware", 2)),
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::OtherApp.CancelOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::OtherApp.CancelMiddleware", 2)))),
                    ImmutableArray.Create(new PolicyFinding(
                        "global::OtherApp.CancelPolicy",
                        ImmutableArray.Create(new MiddlewareRef("global::OtherApp.CancelPolicyMiddleware", 2)),
                        ImmutableArray.Create("global::OtherApp.CancelOrder"))))));

        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            hostBootstrap);

        var pipelineSource = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_AppContext.g.cs");

        Assert.Contains("global::ExternalApp.CreateOrder", pipelineSource.Content);
        Assert.Contains("global::ExternalApp.GlobalMiddleware", pipelineSource.Content);
        Assert.DoesNotContain("global::OtherApp.CancelOrder", pipelineSource.Content);
        Assert.DoesNotContain("global::OtherApp.GlobalMiddleware", pipelineSource.Content);
        Assert.DoesNotContain("global::OtherApp.CancelMiddleware", pipelineSource.Content);
        Assert.DoesNotContain("global::OtherApp.CancelPolicyMiddleware", pipelineSource.Content);
    }

    [Fact]
    public void Generate_ignores_referenced_command_contributions_from_mismatched_contribution_context()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var pipeline = new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(
            discovery,
            pipeline,
            Referenced(
                ImmutableArray.Create(
                    ReferencedHandler(
                        "MixedExternalApp",
                        null,
                        "global::ExternalApp.CreateOrder",
                        "global::ExternalApp.CreateOrderHandler",
                        "global::MyApp.AppContext"),
                    ReferencedHandler(
                        "MixedExternalApp",
                        null,
                        "global::ExternalApp.CancelOrder",
                        "global::ExternalApp.CancelOrderHandler",
                        "global::OtherApp.OtherContext")),
                new ReferencedAssemblyContribution(
                    "MixedExternalApp",
                    null,
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(
                        new PerCommandMiddlewareFinding(
                            "global::ExternalApp.CreateOrder",
                            ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.OrderMiddleware", 2)),
                            "global::MyApp.AppContext"),
                        new PerCommandMiddlewareFinding(
                            "global::ExternalApp.CancelOrder",
                            ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.CancelMiddleware", 2)),
                            "global::OtherApp.OtherContext")),
                    ImmutableArray.Create(
                        new PolicyFinding(
                            "global::ExternalApp.MixedPolicy",
                            ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.PolicyMiddleware", 2)),
                            ImmutableArray.Create("global::ExternalApp.CreateOrder"),
                            "global::MyApp.AppContext"),
                        new PolicyFinding(
                            "global::ExternalApp.CancelPolicy",
                            ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.CancelPolicyMiddleware", 2)),
                            ImmutableArray.Create("global::ExternalApp.CancelOrder"),
                            "global::OtherApp.OtherContext")))));
        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            hostBootstrap);

        var pipelineSource = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_AppContext.g.cs");

        Assert.Contains("global::ExternalApp.CreateOrder", pipelineSource.Content);
        Assert.Contains("global::ExternalApp.OrderMiddleware", pipelineSource.Content);
        Assert.Contains("global::ExternalApp.PolicyMiddleware", pipelineSource.Content);
        Assert.DoesNotContain("global::ExternalApp.CancelOrder", pipelineSource.Content);
        Assert.DoesNotContain("global::ExternalApp.CancelMiddleware", pipelineSource.Content);
        Assert.DoesNotContain("global::ExternalApp.CancelPolicyMiddleware", pipelineSource.Content);
    }

    [Fact]
    public void Generate_emits_pipeline_source_for_each_host_context()
    {
        var context = new CapturingGeneratorContext();
        var createOrder = new HandlerContract(
            MessageTypeFqn: "global::MyApp.CreateOrder",
            HandlerTypeFqn: "global::MyApp.CreateOrderHandler",
            ContextTypeFqn: "global::MyApp.CtxA");
        var cancelOrder = new HandlerContract(
            MessageTypeFqn: "global::MyApp.CancelOrder",
            HandlerTypeFqn: "global::MyApp.CancelOrderHandler",
            ContextTypeFqn: "global::MyApp.CtxB");
        var discovery = new DiscoveryResult(
            ImmutableArray.Create(createOrder, cancelOrder),
            ImmutableArray<QueryHandlerContract>.Empty);
        var pipelineA = new PipelineConfig(
            ImmutableArray.Create(new MiddlewareRef("global::MyApp.AuditA`2", 2)),
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
        var pipelineB = new PipelineConfig(
            ImmutableArray.Create(new MiddlewareRef("global::MyApp.AuditB`2", 2)),
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = new GeneratorExtraction(
            discovery,
            ReferencedAssemblyContributions.Empty,
            ImmutableArray.Create(
                new ContextPipelineConfig("global::MyApp.CtxA", pipelineA),
                new ContextPipelineConfig("global::MyApp.CtxB", pipelineB)));
        var hostBootstrap = HostBootstrap(
            "global::MyApp.CtxA",
            "global::MyApp.CtxA",
            "global::MyApp.CtxB");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: null),
            extraction,
            hostBootstrap);

        var pipelineAContent = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_CtxA.g.cs").Content;
        var pipelineBContent = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_CtxB.g.cs").Content;
        var contributionContent = Assert.Single(
            context.Sources,
            source => source.HintName == "ThisAssemblyContribution.g.cs").Content;

        Assert.Contains(
            "global::TinyDispatcher.ICommandPipeline<global::MyApp.CreateOrder, global::MyApp.CtxA>",
            pipelineAContent);
        Assert.DoesNotContain("global::MyApp.CancelOrder", pipelineAContent);
        Assert.Contains(
            "global::TinyDispatcher.ICommandPipeline<global::MyApp.CancelOrder, global::MyApp.CtxB>",
            pipelineBContent);
        Assert.DoesNotContain("global::MyApp.CreateOrder", pipelineBContent);
        Assert.Contains("AddGeneratedPipelines_MyApp_CtxA(services);", contributionContent);
        Assert.Contains("AddGeneratedPipelines_MyApp_CtxB(services);", contributionContent);
    }

    [Fact]
    public void Generate_emits_module_initializer_when_only_second_context_has_pipeline()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var emptyPipeline = PipelineConfig.Empty;
        var pipelineB = new PipelineConfig(
            ImmutableArray.Create(new MiddlewareRef("global::MyApp.AuditB`2", 2)),
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = new GeneratorExtraction(
            discovery,
            ReferencedAssemblyContributions.Empty,
            ImmutableArray.Create(
                new ContextPipelineConfig("global::MyApp.CtxA", emptyPipeline),
                new ContextPipelineConfig("global::MyApp.CtxB", pipelineB)));
        var hostBootstrap = HostBootstrap(
            "global::MyApp.CtxA",
            "global::MyApp.CtxA",
            "global::MyApp.CtxB");

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: null),
            extraction,
            hostBootstrap);

        Assert.Contains(
            context.Sources,
            source => source.HintName == "DispatcherModuleInitializer.g.cs");
        Assert.Contains(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.MyApp_CtxB.g.cs");
    }

    private static CSharpCompilation CreateCompilation()
    {
        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("namespace MyApp { public sealed class AppContext { } }") },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ICommandMiddleware<,>).Assembly.Location)
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static DiscoveryResult EmptyDiscovery()
    {
        return new DiscoveryResult(
            ImmutableArray<HandlerContract>.Empty,
            ImmutableArray<QueryHandlerContract>.Empty);
    }

    private static GeneratorOptions Options(string? commandContextType)
    {
        return new GeneratorOptions(
            GeneratedNamespace: "TinyDispatcher.Generated",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: null,
            CommandContextType: commandContextType,
            EmitPipelineMap: false,
            PipelineMapFormat: null);
    }

    private static HostBootstrapInfo HostBootstrap(
        string expectedContextFqn,
        params string[] contextFqns)
    {
        var effectiveContexts = contextFqns.Length == 0
            ? new[] { expectedContextFqn }
            : contextFqns;
        var calls = ImmutableArray.CreateBuilder<UseTinyDispatcherCall>(effectiveContexts.Length);
        var contexts = ImmutableArray.CreateBuilder<HostContextInfo>(effectiveContexts.Length);

        for (var i = 0; i < effectiveContexts.Length; i++)
        {
            var contextFqn = effectiveContexts[i];
            var call = new UseTinyDispatcherCall(contextFqn, Location.None);

            calls.Add(call);
            contexts.Add(new HostContextInfo(
                contextFqn,
                ImmutableArray.Create(call)));
        }

        return new HostBootstrapInfo(
            IsHostProject: true,
            ExpectedContextFqn: expectedContextFqn,
            UseTinyDispatcherCalls: calls.ToImmutable(),
            Contexts: contexts.ToImmutable());
    }

    private static ReferencedAssemblyContributions Referenced(params ReferencedAssemblyContribution[] assemblies)
        => new(
            ImmutableArray.Create(assemblies),
            ImmutableArray<ReferencedHandlerContribution>.Empty);

    private static ReferencedAssemblyContributions Referenced(
        ImmutableArray<ReferencedHandlerContribution> handlers,
        params ReferencedAssemblyContribution[] assemblies)
    {
        return new ReferencedAssemblyContributions(
            ImmutableArray.Create(assemblies),
            handlers);
    }

    private static ReferencedHandlerContribution ReferencedHandler(
        string assemblyName,
        string? assemblyContextTypeFqn,
        string messageTypeFqn,
        string handlerTypeFqn,
        string contextTypeFqn)
    {
        return new ReferencedHandlerContribution(
            assemblyName,
            assemblyContextTypeFqn,
            new HandlerContract(
                messageTypeFqn,
                handlerTypeFqn,
                contextTypeFqn));
    }

    private static GeneratorExtraction Extraction(
        DiscoveryResult discovery,
        PipelineConfig pipeline,
        ReferencedAssemblyContributions? referencedContributions = null)
    {
        return new GeneratorExtraction(
            discovery,
            referencedContributions ?? ReferencedAssemblyContributions.Empty,
            ImmutableArray.Create(new ContextPipelineConfig(
                "global::MyApp.AppContext",
                pipeline)));
    }

    private static bool IsPipelineSource(string hintName)
    {
        return hintName.StartsWith("TinyDispatcherPipeline.", StringComparison.Ordinal);
    }
}
