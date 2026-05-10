using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorContextComposition(
    AssemblyContributionComposition AssemblyContribution,
    HostGenerationComposition HostGeneration,
    ImmutableArray<ContextValidationInput> ValidationContexts);
