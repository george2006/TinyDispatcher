#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class MixedBootstrapDiagnosticsTests
{
    [Fact]
    public void DISP110_when_UseTinyDispatcher_and_UseTinyNoOpContext_are_both_present()
    {
        var source = @"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public sealed class TinyBootstrap { }
}

namespace TinyDispatcher.Context
{
    public readonly struct NoOpContext { }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrap> configure)
            => services;

        public static IServiceCollection UseTinyNoOpContext(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrap> configure)
            => services;
    }
}

namespace ConsoleApp
{
    public sealed class CtxA { }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<CtxA>(_ => { });
            services.UseTinyNoOpContext(_ => { });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.Contains(diagnostics, d => d.Id == "DISP110" && d.Severity == DiagnosticSeverity.Error);
    }

    private static Diagnostic[] Run(string source)
    {
        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return diagnostics.ToArray();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

        refs.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}