#nullable enable

namespace TinyDispatcher.SourceGen.Emitters.ModuleInitializer;

internal sealed record ModuleInitializerPlan(
    string GeneratedNamespace,
    string CoreNamespace,
    bool ShouldEmit);
