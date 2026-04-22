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

public sealed class ContextInferenceTests
{
    [Fact]
    public void ResolveAllUseTinyDispatcherContexts_returns_no_op_context_for_UseTinyNoOpContext()
    {
        var compilation = CreateCompilation("""
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }
}

namespace TinyDispatcher
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyNoOpContext(
            this IServiceCollection services,
            System.Action<object> configure)
            => services;
    }
}

namespace MyApp
{
    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyNoOpContext(_ => { });
        }
    }
}
""");
        var invocations = FindInvocations(compilation);

        var result = new ContextInference().ResolveAllUseTinyDispatcherContexts(invocations, compilation);

        var call = Assert.Single(result);
        Assert.Equal("global::TinyDispatcher.Context.NoOpContext", call.ContextTypeFqn);
    }

    [Fact]
    public void ResolveAllUseTinyDispatcherContexts_returns_generic_context_type()
    {
        var compilation = CreateCompilation("""
using System;
using Microsoft.Extensions.DependencyInjection;

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

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<AppContext>(_ => { });
        }
    }
}
""");
        var invocations = FindInvocations(compilation);

        var result = new ContextInference().ResolveAllUseTinyDispatcherContexts(invocations, compilation);

        var call = Assert.Single(result);
        Assert.Equal("global::MyApp.AppContext", call.ContextTypeFqn);
    }

    [Fact]
    public void ResolveAllUseTinyDispatcherContexts_skips_type_parameter_contexts()
    {
        var compilation = CreateCompilation("""
using System;
using Microsoft.Extensions.DependencyInjection;

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
    public static class Startup
    {
        public static void Configure<TContext>(IServiceCollection services)
        {
            services.UseTinyDispatcher<TContext>(_ => { });
        }
    }
}
""");
        var invocations = FindInvocations(compilation);

        var result = new ContextInference().ResolveAllUseTinyDispatcherContexts(invocations, compilation);

        Assert.Empty(result);
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
