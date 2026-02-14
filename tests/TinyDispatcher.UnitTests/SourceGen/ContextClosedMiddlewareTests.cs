#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

public sealed class ContextClosedMiddlewareTests
{
    [Fact]
    public void Generates_closed_middleware_correctly_for_arity1_and_arity2()
    {
        // Arrange
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public interface ICommand { }

    public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command, TContext context, CancellationToken ct);
    }

    namespace Pipeline
    {
        public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
        {
            ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
        }

        public interface ICommandPipelineInvoker<TCommand, TContext> where TCommand : ICommand
        {
            ValueTask ExecuteAsync(TCommand command, TContext ctx, ICommandHandler<TCommand, TContext> handler, CancellationToken ct = default);
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

    public interface IGlobalCommandPipeline<TCommand, TContext> : Pipeline.ICommandPipelineInvoker<TCommand, TContext>
        where TCommand : ICommand { }

    public interface IPolicyCommandPipeline<TCommand, TContext> : Pipeline.ICommandPipelineInvoker<TCommand, TContext>
        where TCommand : ICommand { }

    public interface ICommandPipeline<TCommand, TContext> : Pipeline.ICommandPipelineInvoker<TCommand, TContext>
        where TCommand : ICommand { }

    public sealed class TinyBootstrapp
    {
        public void UseGlobalMiddleware(Type openMiddleware) { }
        public void UseMiddlewareFor<TCommand>(Type openMiddleware) where TCommand : ICommand { }
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
    public sealed record MyTestContext;

    public sealed record Ping : TinyDispatcher.ICommand;

    public sealed class PingHandler : TinyDispatcher.ICommandHandler<Ping, MyTestContext>
    {
        public Task HandleAsync(Ping command, MyTestContext context, CancellationToken ct) => Task.CompletedTask;
    }

    // arity-2 middleware (classic)
    public sealed class OpenMw<TCommand, TContext> : TinyDispatcher.ICommandMiddleware<TCommand, TContext>
        where TCommand : TinyDispatcher.ICommand
    {
        public ValueTask InvokeAsync(
            TCommand cmd,
            TContext ctx,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(cmd, ctx, ct);
    }

    // arity-1 middleware (context-closed)
    public sealed class ClosedMw<TCommand> : TinyDispatcher.ICommandMiddleware<TCommand, MyTestContext>
        where TCommand : TinyDispatcher.ICommand
    {
        public ValueTask InvokeAsync(
            TCommand cmd,
            MyTestContext ctx,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, MyTestContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(cmd, ctx, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<MyTestContext>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(OpenMw<,>));
                tiny.UseMiddlewareFor<Ping>(typeof(ClosedMw<>));
            });
        }
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var generated = GetGeneratedSource(driver, "TinyDispatcherPipeline.g.cs");

        // Assert: per-command middleware (ClosedMw<>) must be closed as ClosedMw<Ping>
        Assert.Contains("global::ConsoleApp.ClosedMw<global::ConsoleApp.Ping>", generated);

        // Assert: global middleware (OpenMw<,>) must be closed as OpenMw<Ping, MyTestContext>
        Assert.Contains("global::ConsoleApp.OpenMw<global::ConsoleApp.Ping, global::ConsoleApp.MyTestContext>", generated);
    }

    [Fact]
    public void Rejects_closed_middleware_if_context_does_not_match_expected()
    {
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
            ValueTask NextAsync(TCommand command, TContext ctx, System.Threading.CancellationToken ct = default);
        }
    }

    public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        ValueTask InvokeAsync(
            TCommand cmd,
            TContext ctx,
            Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            System.Threading.CancellationToken ct);
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
    public sealed record MyTestContext;
    public sealed record OtherContext;

    public sealed class BadClosedMw<TCommand> : TinyDispatcher.ICommandMiddleware<TCommand, OtherContext>
        where TCommand : TinyDispatcher.ICommand
    {
        public ValueTask InvokeAsync(
            TCommand cmd,
            OtherContext ctx,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, OtherContext> runtime,
            System.Threading.CancellationToken ct)
            => runtime.NextAsync(cmd, ctx, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<MyTestContext>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(BadClosedMw<>));
            });
        }
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // DISP304 expected (invalid context-closed middleware)
        Assert.Contains(diagnostics, d => d.Id == "DISP304");
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

        // Ensure generator assembly is referenced (tests usually already reference it, but keep explicit)
        refs.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GetGeneratedSource(GeneratorDriver driver, string hintName)
    {
        var run = driver.GetRunResult();

        // If generator threw, fail loudly with the actual exception.
        var ex = run.Results.Select(r => r.Exception).FirstOrDefault(e => e != null);
        Assert.True(ex is null, ex?.ToString());

        var generated = run.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => string.Equals(s.HintName, hintName, StringComparison.Ordinal));

        // GeneratedSourceResult is a struct; SourceText == null is the robust "not found" check.
        Assert.True(generated.SourceText != null, $"Generated source '{hintName}' not found.");

        return generated.SourceText!.ToString();
    }
}
