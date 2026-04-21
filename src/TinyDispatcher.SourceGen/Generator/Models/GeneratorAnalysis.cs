using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorAnalysis(
    GeneratorOptions EffectiveOptions,
    HostBootstrapInfo HostBootstrap);
