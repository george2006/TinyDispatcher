using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorContextComposition(
    ThisAssemblyContributionInput ThisAssemblyContribution,
    HostCompositionInput HostComposition,
    ReferencedAssemblyContributions ReferencedContributions,
    ImmutableArray<ContextGenerationInput> GenerationContexts,
    ImmutableArray<ContextValidationInput> ValidationContexts);
