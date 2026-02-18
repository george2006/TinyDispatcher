// TinyDispatcher.SourceGen/Emitters/ModuleInitializer/ModuleInitializerPlan.cs

#nullable enable

namespace TinyDispatcher.SourceGen.Emitters.ModuleInitializer;

internal sealed record ModuleInitializerPlan(
    string GeneratedNamespace,
    string CoreNamespace,
    bool ShouldEmit);
