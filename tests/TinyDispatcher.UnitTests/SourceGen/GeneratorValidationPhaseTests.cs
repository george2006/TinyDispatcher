#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;
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
                ImmutableDictionary<string, PolicySpec>.Empty));

        var analysis = new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: ImmutableArray.Create(invocation),
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: true,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray.Create(new UseTinyDispatcherCall(
                    "global::MyApp.AppContext",
                    Location.None))));

        var result = new GeneratorValidationPhase().Validate(
            analysis.Compilation,
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog());

        Assert.Same(discovery, result.Context.DiscoveryResult);
        Assert.True(result.Context.IsHostProject);
        Assert.Equal("global::MyApp.AppContext", result.Context.ExpectedContextFqn);
        Assert.Single(result.Context.UseTinyDispatcherCalls);
        Assert.Empty(result.Diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_marks_project_as_non_host_when_no_UseTinyDispatcher_calls_exist()
    {
        var extraction = new GeneratorExtraction(
            EmptyDiscovery(),
            new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty));

        var analysis = new GeneratorAnalysis(
            Compilation: CreateCompilation(),
            UseTinyCallsSyntax: ImmutableArray<InvocationExpressionSyntax>.Empty,
            EffectiveOptions: Options(commandContextType: null),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: false,
                ExpectedContextFqn: string.Empty,
                UseTinyDispatcherCalls: ImmutableArray<UseTinyDispatcherCall>.Empty));

        var result = new GeneratorValidationPhase().Validate(
            analysis.Compilation,
            analysis.HostBootstrap,
            extraction,
            new DiagnosticsCatalog());

        Assert.False(result.Context.IsHostProject);
        Assert.Equal(string.Empty, result.Context.ExpectedContextFqn);
        Assert.Empty(result.Context.UseTinyDispatcherCalls);
        Assert.Empty(result.Diagnostics.ToImmutable());
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
}
