#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class ContextConsistencyValidatorTests
{
    [Fact]
    public void DISP110_when_multiple_UseTinyDispatcher_calls_exist()
    {
        var source = @"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    public sealed class TinyBootstrapp
    {
        public void UseGlobalMiddleware(Type openMiddleware) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrapp> tiny)
            => services;
    }
}

namespace ConsoleApp
{
    public sealed class CtxA { }
    public sealed class CtxB { }

    public static class StartupA
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<CtxA>(tiny => { });
        }
    }

    public static class StartupB
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<CtxB>(tiny => { });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.Contains(diagnostics, d => d.Id == "DISP110");
    }

    [Fact]
    public void Does_not_report_DISP110_when_single_UseTinyDispatcher_call_exists()
    {
        var source = @"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    public sealed class TinyBootstrapp
    {
        public void UseGlobalMiddleware(Type openMiddleware) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrapp> tiny)
            => services;
    }
}

namespace ConsoleApp
{
    public sealed class Ctx { }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<Ctx>(tiny => { });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "DISP110");
    }

    // Optional: only keep if your ContextConsistencyValidator emits DISP111 for "host but no context"
    // If this is unstable (depends on options), delete this test or update the ID.
    [Fact]
    public void DISP111_when_host_project_but_context_cannot_be_determined()
    {
        // This intentionally uses a non-closed context type argument (generic parameter) to ensure
        // inference yields nothing usable.
        //
        // NOTE: This compiles because the method is generic; the generator should treat this as host gate
        // but context inference must refuse "T" and trigger the missing-context diagnostic.
        var source = @"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    public sealed class TinyBootstrapp
    {
        public void UseGlobalMiddleware(Type openMiddleware) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrapp> tiny)
            => services;
    }
}

namespace ConsoleApp
{
    public static class Startup
    {
        public static void Configure<T>(IServiceCollection services)
        {
            services.UseTinyDispatcher<T>(tiny => { });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.Contains(diagnostics, d => d.Id == "DISP111");
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
