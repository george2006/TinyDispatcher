#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class MultiContextGenerationTests
{
    [Fact]
    public void Generates_compilable_pipeline_sources_for_multiple_contexts()
    {
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;

namespace ConsoleApp
{
    public sealed class CtxA { }
    public sealed class CtxB { }

    public sealed record CreateOrder : ICommand;
    public sealed record CancelOrder : ICommand;

    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, CtxA>
    {
        public Task HandleAsync(CreateOrder command, CtxA context, CancellationToken ct)
            => Task.CompletedTask;
    }

    public sealed class CancelOrderHandler : ICommandHandler<CancelOrder, CtxB>
    {
        public Task HandleAsync(CancelOrder command, CtxB context, CancellationToken ct)
            => Task.CompletedTask;
    }

    public sealed class AuditMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<CtxA>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(AuditMiddleware<,>));
            });

            services.UseTinyDispatcher<CtxB>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(AuditMiddleware<,>));
            });
        }
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatorErrors = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        var compilationErrors = outputCompilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(generatorErrors.Length == 0, string.Join("\n", generatorErrors.Select(d => d.ToString())));
        Assert.True(compilationErrors.Length == 0, string.Join("\n", compilationErrors.Select(d => d.ToString())));
    }

    [Fact]
    public void Reports_invalid_middleware_from_second_context()
    {
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;

namespace ConsoleApp
{
    public sealed class CtxA { }
    public sealed class CtxB { }
    public sealed class OtherContext { }

    public sealed class BadClosedMiddleware<TCommand> : ICommandMiddleware<TCommand, OtherContext>
        where TCommand : ICommand
    {
        public ValueTask InvokeAsync(
            TCommand command,
            OtherContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, OtherContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<CtxA>(tiny => { });

            services.UseTinyDispatcher<CtxB>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(BadClosedMiddleware<>));
            });
        }
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out var diagnostics);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DISP304");
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
