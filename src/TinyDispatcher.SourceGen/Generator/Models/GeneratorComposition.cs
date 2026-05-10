using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorComposition(
    DiscoveryResult AssemblyContributionDiscovery,
    HostGenerationComposition HostGeneration,
    ImmutableArray<ContextValidationInput> ValidationContexts);
