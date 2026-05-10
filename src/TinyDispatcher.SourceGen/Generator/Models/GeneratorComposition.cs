using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorComposition(
    DiscoveryResult ThisAssemblyContributionDiscovery,
    HostGenerationComposition HostGeneration,
    ImmutableArray<HostContextValidationInput> ValidationContexts);
