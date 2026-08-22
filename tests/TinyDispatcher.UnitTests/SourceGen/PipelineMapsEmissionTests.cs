#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class PipelineMapEmissionTests
{
    [Fact]
    public void EmitPipelineMap_false_does_not_emit_pipeline_map_marker()
    {
        var compilation = CreateCompilationAllRefs(Source(emitMaps: false));

        var (driver, _) = Run(compilation);

        var generatedTexts = driver.GetRunResult()
            .Results.SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToArray();

        Assert.DoesNotContain(
            generatedTexts,
            t => t.Contains("TINYDISPATCHER_PIPELINE_MAP_JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void EmitPipelineMap_true_emits_pipeline_map_marker()
    {
        var compilation = CreateCompilationAllRefs(Source(emitMaps: true));

        var errors = compilation.GetDiagnostics()
    .Where(d => d.Severity == DiagnosticSeverity.Error)
    .Select(d => d.ToString())
    .ToArray();

        Assert.True(errors.Length == 0, string.Join("\n", errors));

        var (driver, _) = Run(compilation);

        var generatedTexts = driver.GetRunResult()
            .Results.SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToArray();

        Assert.Contains(
            generatedTexts,
            t => t.Contains("TINYDISPATCHER_PIPELINE_MAP_JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void EmitPipelineMap_true_emits_one_map_per_context_for_shared_command_type()
    {
        var compilation = CreateCompilationAllRefs(MultiContextSource());

        var (driver, diagnostics) = Run(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var generatedNames = driver.GetRunResult()
            .Results.SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToArray();

        Assert.Contains(
            generatedNames,
            name => name == "PipelineMap.ConsoleApp_CtxA.ConsoleApp_Ping.g.cs");
        Assert.Contains(
            generatedNames,
            name => name == "PipelineMap.ConsoleApp_CtxB.ConsoleApp_Ping.g.cs");
    }

    [Fact]
    public void Attributes_format_emits_compilable_pipeline_metadata()
    {
        var compilation = CreateIsolatedCompilation(AttributesSource());
        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        Assert.DoesNotContain(
            generatorDiagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(
            outputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        var generated = Assert.Single(
            driver.GetRunResult()
                .Results.SelectMany(result => result.GeneratedSources),
            source => source.HintName ==
                "PipelineMap.Attributes.TinyDispatcher_AppContext.g.cs");
        var text = generated.SourceText.ToString();

        Assert.Contains(
            "[assembly: global::TinyDispatcher.TinyDispatcherPipelineMapAttribute(",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "typeof(global::TinyDispatcher.Generated.TinyDispatcherGlobalPipeline_TinyDispatcher_AppContext<>),",
            text,
            StringComparison.Ordinal);
        Assert.Contains("typeof(global::Ping),", text, StringComparison.Ordinal);
        Assert.Contains(
            "typeof(global::GlobalMiddleware<,>),",
            text,
            StringComparison.Ordinal);
    }

    private static (Microsoft.CodeAnalysis.GeneratorDriver Driver, Microsoft.CodeAnalysis.Diagnostic[] Diagnostics) Run(CSharpCompilation compilation)
    {
        var generator = new Generator();
        Microsoft.CodeAnalysis.GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diags);
        return (driver, diags.ToArray());
    }

    private static CSharpCompilation CreateCompilationAllRefs(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(a.Location))
                .ToList();

        refs.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            "Tests",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateIsolatedCompilation(string source)
    {
        return CSharpCompilation.Create(
            "PipelineMapAttributesTests",
            new[] { CSharpSyntaxTree.ParseText(source) },
            SourceGenCompilationReferences.CurrentDomainWithoutUnitTests(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string Source(bool emitMaps)
    {
       return $@"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;

[assembly: TinyDispatcherGeneratorOptions(
    CommandContextType = typeof(TinyDispatcher.AppContext),
    EmitPipelineMap = {(emitMaps ? "true" : "false")},
    PipelineMapFormat = ""json""
)]

public sealed record Ping(Guid Id) : ICommand;

public sealed class PingHandler : ICommandHandler<Ping,TinyDispatcher.AppContext>
{{
    public Task HandleAsync(Ping command, TinyDispatcher.AppContext context, CancellationToken ct = default) => Task.CompletedTask;
}}

public static class Boot
{{
    public static void Configure(IServiceCollection services)
    {{
        // host/bootstrap signal for the generator
        services.UseTinyDispatcher<TinyDispatcher.AppContext>(tiny => {{ }});
    }}
}}
";
    }

    private static string MultiContextSource()
    {
        return @"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;

[assembly: TinyDispatcherGeneratorOptions(
    EmitPipelineMap = true,
    PipelineMapFormat = ""json""
)]

namespace ConsoleApp
{
    public sealed class CtxA { }
    public sealed class CtxB { }
    public sealed record Ping : ICommand;

    public sealed class PingHandlerA : ICommandHandler<Ping, CtxA>
    {
        public Task HandleAsync(Ping command, CtxA context, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    public sealed class PingHandlerB : ICommandHandler<Ping, CtxB>
    {
        public Task HandleAsync(Ping command, CtxB context, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    public static class Boot
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<CtxA>(tiny => { });
            services.UseTinyDispatcher<CtxB>(tiny => { });
        }
    }
}
";
    }

    private static string AttributesSource()
    {
        return @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;

[assembly: TinyDispatcherGeneratorOptions(
    CommandContextType = typeof(TinyDispatcher.AppContext),
    EmitPipelineMap = true,
    PipelineMapFormat = ""attributes""
)]

public sealed record Ping(Guid Id) : ICommand;

public sealed class PingHandler : ICommandHandler<Ping, TinyDispatcher.AppContext>
{
    public Task HandleAsync(
        Ping command,
        TinyDispatcher.AppContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class GlobalMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken cancellationToken)
        => runtime.NextAsync(command, context, cancellationToken);
}

public static class Boot
{
    public static void Configure(IServiceCollection services)
    {
        services.UseTinyDispatcher<TinyDispatcher.AppContext>(tiny =>
            tiny.UseGlobalMiddleware(typeof(GlobalMiddleware<,>)));
    }
}
";
    }
}
