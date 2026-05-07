#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using TinyDispatcher.SourceGen.Generator.Validation;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorValidationPhaseTests
{
    [Fact]
    public void Validate_builds_validation_context_from_analysis_and_extraction()
    {
        var compilation = CreateCompilation();
        var invocation = ParseInvocation("services.UseTinyDispatcher<MyApp.AppContext>(_ => { })");
        var discovery = EmptyDiscovery();
        var extraction = new GeneratorExtraction(
            discovery,
            new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty),
            ReferencedAssemblyContributions.Empty);

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: true,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray.Create(new UseTinyDispatcherCall(
                    "global::MyApp.AppContext",
                    Location.None))));

        var result = new GeneratorValidationPhase().Validate(
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Same(discovery, result.Context.DiscoveryResult);
        Assert.True(result.Context.IsHostProject);
        Assert.Equal("global::MyApp.AppContext", result.Context.ExpectedContextFqn);
        Assert.Same(extraction.ReferencedContributions, result.Context.ReferencedContributions);
        Assert.Single(result.Context.UseTinyDispatcherCalls);
        Assert.Empty(result.Diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_marks_project_as_non_host_when_no_UseTinyDispatcher_calls_exist()
    {
        var compilation = CreateCompilation();
        var extraction = new GeneratorExtraction(
            EmptyDiscovery(),
            new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty),
            ReferencedAssemblyContributions.Empty);

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: null),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: false,
                ExpectedContextFqn: string.Empty,
                UseTinyDispatcherCalls: ImmutableArray<UseTinyDispatcherCall>.Empty));

        var result = new GeneratorValidationPhase().Validate(
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.False(result.Context.IsHostProject);
        Assert.Equal(string.Empty, result.Context.ExpectedContextFqn);
        Assert.Same(extraction.ReferencedContributions, result.Context.ReferencedContributions);
        Assert.Empty(result.Context.UseTinyDispatcherCalls);
        Assert.Empty(result.Diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_does_not_merge_referenced_contributions_for_non_host_projects()
    {
        var compilation = CreateCompilation();
        var discovery = EmptyDiscovery();
        var extraction = new GeneratorExtraction(
            discovery,
            PipelineConfig.Empty,
            Referenced(new ReferencedAssemblyContribution(
                "ExternalApp",
                "global::MyApp.AppContext",
                ImmutableArray.Create(new HandlerContract(
                    "global::ExternalApp.CreateOrder",
                    "global::ExternalApp.CreateOrderHandler",
                    "global::MyApp.AppContext")),
                ImmutableArray.Create(new PerCommandMiddlewareFinding(
                    "global::ExternalApp.MissingCommand",
                    ImmutableArray<MiddlewareRef>.Empty)),
                ImmutableArray.Create(new PolicyFinding(
                    "global::ExternalApp.OrderPolicy",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create("global::ExternalApp.OtherMissingCommand"))))));

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: null),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: false,
                ExpectedContextFqn: string.Empty,
                UseTinyDispatcherCalls: ImmutableArray<UseTinyDispatcherCall>.Empty));

        var result = new GeneratorValidationPhase().Validate(
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Same(discovery, result.Context.DiscoveryResult);
        Assert.Same(extraction.Pipeline, result.Context.Pipeline);
        Assert.Empty(result.Diagnostics.ToImmutable());
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
        var extraction = new GeneratorExtraction(
            discovery,
            PipelineConfig.Empty,
            Referenced(new ReferencedAssemblyContribution(
                "ExternalApp",
                "global::MyApp.AppContext",
                ImmutableArray.Create(new HandlerContract(
                    "global::MyApp.CreateOrder",
                    "global::ExternalApp.CreateOrderHandler",
                    "global::MyApp.AppContext")),
                ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                ImmutableArray<PolicyFinding>.Empty)));

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: true,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray.Create(new UseTinyDispatcherCall(
                    "global::MyApp.AppContext",
                    Location.None))));

        var result = new GeneratorValidationPhase().Validate(
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        var diagnostic = Assert.Single(result.Diagnostics.ToImmutable());
        Assert.Equal("DISP101", diagnostic.Id);
        Assert.Equal(2, result.Context.DiscoveryResult.Commands.Length);
    }

    [Fact]
    public void Validate_reports_unknown_command_warnings_for_referenced_pipeline_contributions()
    {
        var compilation = CreateCompilation();
        var extraction = new GeneratorExtraction(
            EmptyDiscovery(),
            PipelineConfig.Empty,
            Referenced(new ReferencedAssemblyContribution(
                "ExternalApp",
                "global::MyApp.AppContext",
                ImmutableArray<HandlerContract>.Empty,
                ImmutableArray.Create(new PerCommandMiddlewareFinding(
                    "global::ExternalApp.MissingCommand",
                    ImmutableArray<MiddlewareRef>.Empty)),
                ImmutableArray.Create(new PolicyFinding(
                    "global::ExternalApp.OrderPolicy",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create("global::ExternalApp.OtherMissingCommand"))))));

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: true,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray.Create(new UseTinyDispatcherCall(
                    "global::MyApp.AppContext",
                    Location.None))));

        var result = new GeneratorValidationPhase().Validate(
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog(),
            ValidationRoslynDependencies.Create(compilation));

        Assert.Collection(
            result.Diagnostics.ToImmutable(),
            diagnostic => Assert.Equal("DISP410", diagnostic.Id),
            diagnostic => Assert.Equal("DISP411", diagnostic.Id));
        Assert.True(result.Context.PerCommand.ContainsKey("global::ExternalApp.MissingCommand"));
        Assert.True(result.Context.Policies.ContainsKey("global::ExternalApp.OrderPolicy"));
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

    private static InvocationExpressionSyntax ParseInvocation(string expression)
    {
        return Assert.IsType<InvocationExpressionSyntax>(SyntaxFactory.ParseExpression(expression));
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
    {
        return new ReferencedAssemblyContributions(ImmutableArray.Create(assemblies));
    }
}
