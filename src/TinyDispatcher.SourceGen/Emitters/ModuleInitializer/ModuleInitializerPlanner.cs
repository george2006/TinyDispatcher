#nullable enable

using System;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.ModuleInitializer;

internal static class ModuleInitializerPlanner
{
    public static ModuleInitializerPlan Build(DiscoveryResult discovery, GeneratorOptions options)
    {
        if (discovery is null) throw new ArgumentNullException(nameof(discovery));

        // Keep existing behavior: no handlers => do not emit module initializer.
        var hasHandlers = discovery.Commands.Length > 0 || discovery.Queries.Length > 0;

        return new ModuleInitializerPlan(
            GeneratedNamespace: options.GeneratedNamespace,
            CoreNamespace: Known.CoreNamespace,
            ShouldEmit: hasHandlers);
    }
}
