#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

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
}