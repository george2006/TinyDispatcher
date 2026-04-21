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
using TinyDispatcher.SourceGen.Validation;
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
                        ImmutableArray<string>.Empty))));

        var analysis = new GeneratorAnalysis(
            EffectiveOptions: Options(commandContextType: "MyApp.AppContext"),
            HostBootstrap: new HostBootstrapInfo(
                IsHostProject: false,
                ExpectedContextFqn: "global::MyApp.AppContext",
                UseTinyDispatcherCalls: ImmutableArray<UseTinyDispatcherCall>.Empty));

        var validation = new GeneratorValidationResult(
            Context: new GeneratorValidationContext.Builder(
                    compilation,
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

}
