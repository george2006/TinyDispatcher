#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

public sealed class MiddlewareRefShapeValidatorTests
{
    [Fact]
    public void DISP301_when_middleware_is_not_open_generic_type_definition()
    {
        // typeof(NonGenericMw) is not open generic => DISP301
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    namespace Pipeline
    {
        public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
        {
            ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
        }
    }

    public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        ValueTask InvokeAsync(
            TCommand cmd,
            TContext ctx,
            Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct);
    }

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
    public sealed class MyCtx { }
    public sealed class Ping : TinyDispatcher.ICommand { }

    // Not generic at all
    public sealed class NonGenericMw : TinyDispatcher.ICommandMiddleware<Ping, MyCtx>
    {
        public ValueTask InvokeAsync(
            Ping cmd,
            MyCtx ctx,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<Ping, MyCtx> runtime,
            CancellationToken ct)
            => runtime.NextAsync(cmd, ctx, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<MyCtx>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(NonGenericMw));
            });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.Contains(diagnostics, d => d.Id == "DISP301");
    }

    [Fact]
    public void DISP302_when_middleware_arity_is_not_1_or_2()
    {
        // typeof(Mw<,,>) => arity 3 => DISP302
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    namespace Pipeline
    {
        public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
        {
            ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
        }
    }

    public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        ValueTask InvokeAsync(
            TCommand cmd,
            TContext ctx,
            Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct);
    }

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
    public sealed class MyCtx { }
    public sealed class Ping : TinyDispatcher.ICommand { }

    // Arity 3 (invalid)
    public sealed class Mw<TCommand, TContext, TOther> : TinyDispatcher.ICommandMiddleware<TCommand, TContext>
        where TCommand : TinyDispatcher.ICommand
    {
        public ValueTask InvokeAsync(
            TCommand cmd,
            TContext ctx,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(cmd, ctx, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<MyCtx>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(Mw<,,>));
            });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.Contains(diagnostics, d => d.Id == "DISP302");
    }

    [Fact]
    public void DISP304_when_context_closed_middleware_context_does_not_match_expected()
    {
        // Expected context is MyCtx (from UseTinyDispatcher<MyCtx>)
        // Middleware implements ICommandMiddleware<TCommand, OtherCtx> => DISP304
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    namespace Pipeline
    {
        public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
        {
            ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
        }
    }

    public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        ValueTask InvokeAsync(
            TCommand cmd,
            TContext ctx,
            Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct);
    }

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
    public sealed class MyCtx { }
    public sealed class OtherCtx { }

    // Arity 1 (context-closed), but wrong context type (OtherCtx)
    public sealed class BadClosedMw<TCommand> : TinyDispatcher.ICommandMiddleware<TCommand, OtherCtx>
        where TCommand : TinyDispatcher.ICommand
    {
        public ValueTask InvokeAsync(
            TCommand cmd,
            OtherCtx ctx,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, OtherCtx> runtime,
            CancellationToken ct)
            => runtime.NextAsync(cmd, ctx, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<MyCtx>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(BadClosedMw<>));
            });
        }
    }
}
";

        var diagnostics = Run(source);

        Assert.Contains(diagnostics, d => d.Id == "DISP304");
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
