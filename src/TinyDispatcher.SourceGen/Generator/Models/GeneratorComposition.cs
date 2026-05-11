using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorComposition(
    AssemblyContributionComposition AssemblyContribution,
    HostGenerationComposition HostGeneration,
    ImmutableArray<HostContextValidationInput> ValidationContexts);
