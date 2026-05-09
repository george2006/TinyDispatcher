#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Generator.Extraction;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class PipelineConfigExtractorTests
{
    [Fact]
    public void ExtractByContext_keeps_bootstrap_pipeline_config_scoped_to_each_context()
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
    public sealed class TinyBootstrap
    {
        public void UseGlobalMiddleware(Type middlewareType) { }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyBootstrap> configure)
            => services;
    }
}

namespace MyApp
{
    public sealed class AppContext { }
    public sealed class OtherContext { }
    public sealed class AppMiddleware<TCommand, TContext> { }
    public sealed class OtherMiddleware<TCommand, TContext> { }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<AppContext>(
                tiny => tiny.UseGlobalMiddleware(typeof(AppMiddleware<,>)));

            services.UseTinyDispatcher<OtherContext>(
                tiny => tiny.UseGlobalMiddleware(typeof(OtherMiddleware<,>)));
        }
    }
}
""");
        var confirmedCalls = GetConfirmedCalls(compilation);
        var lambdas = new BootstrapLambdaExtractor().Extract(compilation, confirmedCalls);

        var contexts = new PipelineConfigExtractor().ExtractByContext(lambdas);

        Assert.Collection(
            contexts,
            context =>
            {
                Assert.Equal("global::MyApp.AppContext", context.ContextTypeFqn);
                var middleware = Assert.Single(context.Pipeline.Globals);
                Assert.Equal("global::MyApp.AppMiddleware", middleware.OpenTypeFqn);
            },
            context =>
            {
                Assert.Equal("global::MyApp.OtherContext", context.ContextTypeFqn);
                var middleware = Assert.Single(context.Pipeline.Globals);
                Assert.Equal("global::MyApp.OtherMiddleware", middleware.OpenTypeFqn);
            });
    }

    private static ImmutableArray<InvocationExpressionSyntax> GetConfirmedCalls(Compilation compilation)
    {
        var candidates = compilation.SyntaxTrees.Single()
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToImmutableArray();

        return new UseTinyDispatcherSemanticFilter().Filter(compilation, candidates);
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
}
