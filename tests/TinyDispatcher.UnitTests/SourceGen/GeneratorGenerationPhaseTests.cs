#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using TinyDispatcher.SourceGen.Generator.Validation;
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
        var extraction = new GeneratorExtraction(
            discovery,
            new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty.Add(
                    "global::MyApp.Policy",
                    new PolicySpec(
                        "global::MyApp.Policy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray<string>.Empty))),
            ReferencedAssemblyContributions.Empty);

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: false,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray<UseTinyDispatcherCall>.Empty));

        var validation = new GeneratorValidationResult(
            Context: new GeneratorValidationContext.Builder(
                    discovery,
                    new DiagnosticsCatalog())
                .WithHostGate(isHost: false)
                .WithExpectedContext("global::MyApp.AppContext")
                .WithPipelineConfig(extraction.Pipeline)
                .Build(),
            Diagnostics: new DiagnosticBag());

        new GeneratorGenerationPhase().Generate(
            context,
            analysis.EffectiveOptions,
            extraction,
            validation);

        Assert.DoesNotContain(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.g.cs");
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
        var extraction = new GeneratorExtraction(
            discovery,
            new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty),
            ReferencedAssemblyContributions.Empty);
        var validation = new GeneratorValidationResult(
            Context: new GeneratorValidationContext.Builder(
                    discovery,
                    new DiagnosticsCatalog())
                .WithHostGate(isHost: true)
                .WithExpectedContext("global::MyApp.AppContext")
                .WithPipelineConfig(extraction.Pipeline)
                .Build(),
            Diagnostics: new DiagnosticBag());

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: null),
            extraction,
            validation);

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
        var extraction = new GeneratorExtraction(
            discovery,
            new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty),
            Referenced(
                new ReferencedAssemblyContribution(
                    "ExternalApp",
                    "global::MyApp.AppContext",
                    ImmutableArray.Create(new HandlerContract(
                        "global::ExternalApp.CreateOrder",
                        "global::ExternalApp.CreateOrderHandler",
                        "global::MyApp.AppContext")),
                    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                    ImmutableDictionary<string, PolicySpec>.Empty)));

        var validation = new GeneratorValidationResult(
            Context: new GeneratorValidationContext.Builder(
                    discovery,
                    new DiagnosticsCatalog())
                .WithHostGate(isHost: true)
                .WithExpectedContext("global::MyApp.AppContext")
                .WithPipelineConfig(extraction.Pipeline)
                .Build(),
            Diagnostics: new DiagnosticBag());

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            validation);

        var pipeline = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.g.cs");

        Assert.Contains(
            "global::TinyDispatcher.ICommandPipeline<global::ExternalApp.CreateOrder, global::MyApp.AppContext>",
            pipeline.Content);
        Assert.Contains(
            "TinyDispatcherGlobalPipeline<global::ExternalApp.CreateOrder>",
            pipeline.Content);
    }

    [Fact]
    public void Generate_merges_referenced_per_command_and_policy_contributions_into_pipeline_emission()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var extraction = new GeneratorExtraction(
            discovery,
            new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty),
            Referenced(
                new ReferencedAssemblyContribution(
                    "ExternalApp",
                    "global::MyApp.AppContext",
                    ImmutableArray.Create(new HandlerContract(
                        "global::ExternalApp.CreateOrder",
                        "global::ExternalApp.CreateOrderHandler",
                        "global::MyApp.AppContext")),
                    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                        "global::ExternalApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.OrderMiddleware", 2))),
                    ImmutableDictionary<string, PolicySpec>.Empty.Add(
                        "global::ExternalApp.OrderPolicy",
                        new PolicySpec(
                            "global::ExternalApp.OrderPolicy",
                            ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.PolicyMiddleware", 2)),
                            ImmutableArray.Create("global::ExternalApp.CreateOrder"))))));

        var validation = new GeneratorValidationResult(
            Context: new GeneratorValidationContext.Builder(
                    discovery,
                    new DiagnosticsCatalog())
                .WithHostGate(isHost: true)
                .WithExpectedContext("global::MyApp.AppContext")
                .WithPipelineConfig(extraction.Pipeline)
                .Build(),
            Diagnostics: new DiagnosticBag());

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            validation);

        var pipeline = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.g.cs");

        Assert.Contains(
            "internal sealed class TinyDispatcherPipeline_CreateOrder",
            pipeline.Content);
        Assert.Contains(
            "global::ExternalApp.PolicyMiddleware",
            pipeline.Content);
        Assert.Contains(
            "global::ExternalApp.OrderMiddleware",
            pipeline.Content);
    }

    [Fact]
    public void Generate_ignores_referenced_contributions_from_mismatched_context_assemblies()
    {
        var context = new CapturingGeneratorContext();
        var discovery = EmptyDiscovery();
        var extraction = new GeneratorExtraction(
            discovery,
            new PipelineConfig(
                ImmutableArray.Create(new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2)),
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty),
            Referenced(
                new ReferencedAssemblyContribution(
                    "Matching",
                    "global::MyApp.AppContext",
                    ImmutableArray.Create(new HandlerContract(
                        "global::ExternalApp.CreateOrder",
                        "global::ExternalApp.CreateOrderHandler",
                        "global::MyApp.AppContext")),
                    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                        "global::ExternalApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::ExternalApp.OrderMiddleware", 2))),
                    ImmutableDictionary<string, PolicySpec>.Empty),
                new ReferencedAssemblyContribution(
                    "Mismatched",
                    "global::OtherApp.OtherContext",
                    ImmutableArray.Create(new HandlerContract(
                        "global::OtherApp.CancelOrder",
                        "global::OtherApp.CancelOrderHandler",
                        "global::OtherApp.OtherContext")),
                    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                        "global::OtherApp.CancelOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::OtherApp.CancelMiddleware", 2))),
                    ImmutableDictionary<string, PolicySpec>.Empty.Add(
                        "global::OtherApp.CancelPolicy",
                        new PolicySpec(
                            "global::OtherApp.CancelPolicy",
                            ImmutableArray.Create(new MiddlewareRef("global::OtherApp.CancelPolicyMiddleware", 2)),
                            ImmutableArray.Create("global::OtherApp.CancelOrder"))))));

        var validation = new GeneratorValidationResult(
            Context: new GeneratorValidationContext.Builder(
                    discovery,
                    new DiagnosticsCatalog())
                .WithHostGate(isHost: true)
                .WithExpectedContext("global::MyApp.AppContext")
                .WithPipelineConfig(extraction.Pipeline)
                .Build(),
            Diagnostics: new DiagnosticBag());

        new GeneratorGenerationPhase().Generate(
            context,
            Options(commandContextType: "global::MyApp.AppContext"),
            extraction,
            validation);

        var pipeline = Assert.Single(
            context.Sources,
            source => source.HintName == "TinyDispatcherPipeline.g.cs");

        Assert.Contains("global::ExternalApp.CreateOrder", pipeline.Content);
        Assert.DoesNotContain("global::OtherApp.CancelOrder", pipeline.Content);
        Assert.DoesNotContain("global::OtherApp.CancelMiddleware", pipeline.Content);
        Assert.DoesNotContain("global::OtherApp.CancelPolicyMiddleware", pipeline.Content);
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

    private static ReferencedAssemblyContributions Referenced(params ReferencedAssemblyContribution[] assemblies)
        => new(ImmutableArray.Create(assemblies));

}
