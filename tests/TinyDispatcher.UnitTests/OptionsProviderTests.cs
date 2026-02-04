#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher;
using TinyDispatcher.SourceGen.Internal;
using Xunit;

namespace TinyDispatcher.UnitTets;

public sealed class OptionsProviderTests
{
    [Fact]
    public void Reads_options_from_assembly_attribute_even_when_optionsProvider_is_null()
    {
        var source = @"
using System;
using TinyDispatcher;

[assembly: TinyDispatcherGeneratorOptions(
    CoreNamespace = ""Acme.Core"",
    GeneratedNamespace = ""Acme.Gen"",
    IncludeNamespacePrefix = ""Acme."",
    CommandContextType = typeof(MyCtx),
    EmitPipelineMap = true,
    PipelineMapFormat = ""json""
)]

public sealed class MyCtx {}
";

        var compilation = CreateCompilation(source);

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);

        var sut = new OptionsProvider();

        var opts = sut.GetOptions(compilation, optionsProvider: null);

        Assert.NotNull(opts);
        Assert.Equal("Acme.Core", opts!.CoreNamespace);
        Assert.Equal("Acme.Gen", opts.GeneratedNamespace);
        Assert.Equal("Acme.", opts.IncludeNamespacePrefix);

        Assert.True(opts.EmitPipelineMap);
        Assert.Equal("json", opts.PipelineMapFormat);

        Assert.False(string.IsNullOrWhiteSpace(opts.CommandContextType));
        Assert.StartsWith("global::", opts.CommandContextType!, StringComparison.Ordinal);
        Assert.Contains("MyCtx", opts.CommandContextType!, StringComparison.Ordinal);
    }

    private static Compilation CreateCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var tpa = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var refs = tpa.Split(Path.PathSeparator)
                      .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                      .ToList();

        refs.Add(MetadataReference.CreateFromFile(typeof(TinyDispatcherGeneratorOptionsAttribute).Assembly.Location));

        return CSharpCompilation.Create(
            "Tests",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
