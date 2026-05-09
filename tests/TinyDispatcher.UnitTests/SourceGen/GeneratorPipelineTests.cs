#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorPipelineTests
{
    [Fact]
    public void Execute_reports_missing_context_validation_errors_and_stops_generation()
    {
        var compilation = CreateCompilation(@"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<object> tiny)
            => services;
    }
}

namespace MyApp
{
    public static class Startup
    {
        public static void Configure<T>(IServiceCollection services)
        {
            services.UseTinyDispatcher<T>(_ => { });
        }
    }
}
");
        var input = GeneratorInput.Create(
            compilation,
            ImmutableArray<INamedTypeSymbol?>.Empty,
            FindUseTinyDispatcherCalls(compilation.SyntaxTrees.Single()),
            EmptyAnalyzerConfigOptionsProvider.Instance);
        var context = new CapturingGeneratorContext();

        new GeneratorPipeline().Execute(context, input);

        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Id == "DISP111");
        Assert.Empty(context.Sources);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToArray();

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<InvocationExpressionSyntax?> FindUseTinyDispatcherCalls(SyntaxTree tree)
    {
        var syntax = new UseTinyDispatcherSyntax();

        return tree.GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(syntax.IsUseTinyDispatcherInvocation)
            .Cast<InvocationExpressionSyntax?>()
            .ToImmutableArray();
    }

}
