#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen.Generator.Analysis;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorAnalysisPhaseTests
{
    [Fact]
    public void Analyze_groups_bootstrap_calls_by_context_in_source_order()
    {
        var compilation = CreateCompilation("""
using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }
}

namespace TinyDispatcher
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<object> configure)
            => services;
    }
}

namespace MyApp
{
    public sealed class AppContext { }
    public sealed class OtherContext { }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<AppContext>(_ => { });
            services.UseTinyDispatcher<OtherContext>(_ => { });
            services.UseTinyDispatcher<AppContext>(_ => { });
        }
    }
}
""");
        var invocations = FindInvocations(compilation);

        var result = GeneratorAnalysisPhase.Analyze(
            compilation,
            invocations,
            EmptyAnalyzerConfigOptionsProvider.Instance);

        Assert.Collection(
            result.Analysis.HostBootstrap.Contexts,
            context =>
            {
                Assert.Equal("global::MyApp.AppContext", context.ContextTypeFqn);
                Assert.Equal(2, context.UseTinyDispatcherCalls.Length);
            },
            context =>
            {
                Assert.Equal("global::MyApp.OtherContext", context.ContextTypeFqn);
                Assert.Single(context.UseTinyDispatcherCalls);
            });
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
                .Cast<MetadataReference>()
                .ToArray();

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<InvocationExpressionSyntax> FindInvocations(Compilation compilation)
    {
        return compilation.SyntaxTrees.Single()
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToImmutableArray();
    }
}
