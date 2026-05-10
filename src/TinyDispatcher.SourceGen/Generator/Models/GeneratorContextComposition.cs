using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorContextComposition(
    DiscoveryResult LocalDiscovery,
    DiscoveryResult Discovery,
    ReferencedAssemblyContributions ReferencedContributions,
    ImmutableArray<ContextGenerationInput> GenerationContexts,
    ImmutableArray<ContextValidationInput> ValidationContexts);
