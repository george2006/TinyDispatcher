#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class BootstrapLambdaSelectionTests
{
    [Fact]
    public void Picks_configure_lambda_when_contextFactory_lambda_is_also_present()
    {
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
    public sealed class TinyBootstrap
    {
        public void UseGlobalMiddleware(Type middlewareType) { }
    }

    public interface ICommand { }

    public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command, TContext context, CancellationToken ct);
    }

    public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct = default);
    }
}

namespace TinyDispatcher.Pipeline
{
    public interface ICommandPipelineRuntime<TCommand, TContext> { }
}

namespace ConsoleApp
{
    public readonly struct Ctx { }
    public sealed class Cmd : TinyDispatcher.ICommand { }

    public sealed class Handler : TinyDispatcher.ICommandHandler<Cmd, Ctx>
    {
        public Task HandleAsync(Cmd command, Ctx context, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class Mw<TCommand, TContext> : TinyDispatcher.ICommandMiddleware<TCommand, TContext>
        where TCommand : TinyDispatcher.ICommand
    {
        public ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct = default)
            => default;
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<Ctx>(
                tiny => { tiny.UseGlobalMiddleware(typeof(Mw<,>)); },
                static (_, __) => new ValueTask<Ctx>(default));
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrap> configure,
            Func<IServiceProvider, CancellationToken, ValueTask<TContext>> contextFactory)
            => services;
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var run = driver.GetRunResult();
        var generatedNames = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToArray();

        Assert.Contains(generatedNames, n => n == "TinyDispatcherPipeline.g.cs");
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