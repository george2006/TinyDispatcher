#nullable enable

using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Internal;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorOptionsFactoryTests
{
    [Fact]
    public void Create_defaults_pipeline_maps_to_disabled_when_no_options_provider_exists()
    {
        var compilation = CSharpCompilation.Create("Tests");
        var sut = new GeneratorOptionsFactory(new OptionsProvider());

        var options = sut.Create(compilation, provider: null!);

        Assert.False(options.EmitPipelineMap);
        Assert.Equal("json", options.PipelineMapFormat);
    }
}
