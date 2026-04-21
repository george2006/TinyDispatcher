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

public sealed class UseTinyDispatcherSemanticFilterTests
{
    [Fact]
    public void Filter_keeps_TinyDispatcher_UseTinyDispatcher_bootstrap_call()
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

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<AppContext>(_ => { });
        }
    }
}
""");
        var candidates = FindCandidateInvocations(compilation);

        var result = new UseTinyDispatcherSemanticFilter().Filter(compilation, candidates);

        Assert.Single(result);
    }

    [Fact]
    public void Filter_rejects_unrelated_same_name_extension_call()
    {
        var compilation = CreateCompilation("""
using System;
using Microsoft.Extensions.DependencyInjection;
using MyFramework;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }
}

namespace MyFramework
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
        var candidates = FindCandidateInvocations(compilation);

        var result = new UseTinyDispatcherSemanticFilter().Filter(compilation, candidates);

        Assert.Empty(result);
    }

    [Fact]
    public void Filter_keeps_TinyDispatcher_UseTinyNoOpContext_bootstrap_call()
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
        public static IServiceCollection UseTinyNoOpContext(
            this IServiceCollection services,
            Action<object> configure)
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
        var candidates = FindCandidateInvocations(compilation);

        var result = new UseTinyDispatcherSemanticFilter().Filter(compilation, candidates);

        Assert.Single(result);
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

    private static ImmutableArray<InvocationExpressionSyntax> FindCandidateInvocations(Compilation compilation)
    {
        var syntax = new UseTinyDispatcherSyntax();

        return compilation.SyntaxTrees.Single()
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(syntax.IsUseTinyDispatcherInvocation)
            .ToImmutableArray();
    }
}
