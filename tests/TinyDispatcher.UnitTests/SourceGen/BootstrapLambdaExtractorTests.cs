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

public sealed class BootstrapLambdaExtractorTests
{
    [Fact]
    public void Extract_attaches_resolved_context_to_each_bootstrap_lambda()
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
        }
    }
}
""");
        var candidates = FindInvocations(compilation);
        var confirmedCalls = new UseTinyDispatcherSemanticFilter().Filter(compilation, candidates);

        var lambdas = new BootstrapLambdaExtractor().Extract(compilation, confirmedCalls);

        Assert.Collection(
            lambdas,
            lambda => Assert.Equal("global::MyApp.AppContext", lambda.ContextTypeFqn),
            lambda => Assert.Equal("global::MyApp.OtherContext", lambda.ContextTypeFqn));
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
