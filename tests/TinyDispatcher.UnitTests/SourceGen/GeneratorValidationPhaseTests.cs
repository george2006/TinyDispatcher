#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Composition;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Validation;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorValidationPhaseTests
{
    [Fact]
    public void Validate_reports_no_diagnostics_for_valid_host_project()
    {
        var compilation = CreateCompilation();
        var discovery = EmptyDiscovery();
        var pipeline = new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(discovery, pipeline);
        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        var diagnostics = new GeneratorValidationPhase().Validate(
            hostBootstrap,
            Composition(hostBootstrap, extraction),
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Empty(diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_marks_project_as_non_host_when_no_UseTinyDispatcher_calls_exist()
    {
        var compilation = CreateCompilation();
        var pipeline = new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty);
        var extraction = Extraction(EmptyDiscovery(), pipeline);
        var hostBootstrap = NonHostBootstrap();

        var diagnostics = new GeneratorValidationPhase().Validate(
            hostBootstrap,
            Composition(hostBootstrap, extraction),
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Empty(diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_does_not_merge_referenced_contributions_for_non_host_projects()
    {
        var compilation = CreateCompilation();
        var discovery = EmptyDiscovery();
        var extraction = Extraction(
            discovery,
            PipelineConfig.Empty,
            Referenced(new ReferencedAssemblyContribution(
                "ExternalApp",
                "global::MyApp.AppContext",
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableArray.Create(new ReferencedPerCommandMiddlewareContribution(
                    "global::ExternalApp.MissingCommand",
                    ImmutableArray<MiddlewareRef>.Empty)),
                ImmutableArray.Create(new ReferencedPolicyContribution(
                    "global::ExternalApp.OrderPolicy",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create("global::ExternalApp.OtherMissingCommand"))))));
        var hostBootstrap = NonHostBootstrap();

        var diagnostics = new GeneratorValidationPhase().Validate(
            hostBootstrap,
            Composition(hostBootstrap, extraction),
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Empty(diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_reports_duplicate_handlers_when_referenced_assembly_contributes_same_command()
    {
        var compilation = CreateCompilation();
        var discovery = new DiscoveryResult(
            ImmutableArray.Create(new HandlerContract(
                "global::MyApp.CreateOrder",
                "global::MyApp.CreateOrderHandler",
                "global::MyApp.AppContext")),
            ImmutableArray<QueryHandlerContract>.Empty);
        var extraction = Extraction(
            discovery,
            PipelineConfig.Empty,
            Referenced(new ReferencedAssemblyContribution(
                "ExternalApp",
                "global::MyApp.AppContext",
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableArray<ReferencedPerCommandMiddlewareContribution>.Empty,
                ImmutableArray<ReferencedPolicyContribution>.Empty,
                ImmutableArray.Create(new HandlerContract(
                    "global::MyApp.CreateOrder",
                    "global::ExternalApp.CreateOrderHandler",
                    "global::MyApp.AppContext")))));
        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        var diagnostics = new GeneratorValidationPhase().Validate(
            hostBootstrap,
            Composition(hostBootstrap, extraction),
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP101", diagnostic.Id);
    }

    [Fact]
    public void Validate_reports_unknown_command_warnings_for_referenced_pipeline_contributions()
    {
        var compilation = CreateCompilation();
        var extraction = Extraction(
            EmptyDiscovery(),
            PipelineConfig.Empty,
            Referenced(new ReferencedAssemblyContribution(
                "ExternalApp",
                "global::MyApp.AppContext",
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableArray.Create(new ReferencedPerCommandMiddlewareContribution(
                    "global::ExternalApp.MissingCommand",
                    ImmutableArray<MiddlewareRef>.Empty)),
                ImmutableArray.Create(new ReferencedPolicyContribution(
                    "global::ExternalApp.OrderPolicy",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create("global::ExternalApp.OtherMissingCommand"))))));
        var hostBootstrap = HostBootstrap("global::MyApp.AppContext");

        var diagnostics = new GeneratorValidationPhase().Validate(
            hostBootstrap,
            Composition(hostBootstrap, extraction),
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Collection(
            diagnostics.ToImmutable(),
            diagnostic => Assert.Equal("DISP410", diagnostic.Id),
            diagnostic => Assert.Equal("DISP411", diagnostic.Id));
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

    private static HostBootstrapInfo HostBootstrap(string contextFqn)
    {
        var call = new UseTinyDispatcherCall(contextFqn, Location.None);

        return new HostBootstrapInfo(
            IsHostProject: true,
            ConfiguredContextFqn: contextFqn,
            Contexts: ImmutableArray.Create(new HostLaneDeclaration(
                contextFqn,
                ImmutableArray.Create(call))));
    }

    private static HostBootstrapInfo NonHostBootstrap()
    {
        return new HostBootstrapInfo(
            IsHostProject: false,
            ConfiguredContextFqn: string.Empty);
    }

    private static ReferencedAssemblyContributions Referenced(params ReferencedAssemblyContribution[] assemblies)
    {
        return new ReferencedAssemblyContributions(ImmutableArray.Create(assemblies));
    }

    private static GeneratorExtraction Extraction(
        DiscoveryResult discovery,
        PipelineConfig pipeline,
        ReferencedAssemblyContributions? referencedContributions = null)
    {
        return new GeneratorExtraction(
            new ThisAssemblyExtraction(
                discovery,
                ImmutableArray.Create(new ContextPipeline(
                    "global::MyApp.AppContext",
                    pipeline))),
            referencedContributions ?? ReferencedAssemblyContributions.Empty);
    }

    private static GeneratorModel Composition(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        return new GeneratorCompositionPhase().Compose(hostBootstrap, extraction);
    }
}
