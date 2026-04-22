#nullable enable

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.ModuleInitializer;

internal sealed record ModuleInitializerPlan(
    string GeneratedNamespace,
    string CoreNamespace,
    bool ShouldEmit);

