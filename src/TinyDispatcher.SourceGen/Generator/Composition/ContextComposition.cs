using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal readonly record struct ContextComposition(
    HostContextInfo HostContext,
    string ContextTypeFqn,
    PipelineConfig ThisAssemblyPipeline,
    DiscoveryResult HostDiscovery,
    PipelineConfig HostPipeline);
