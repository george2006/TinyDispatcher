using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorModel(
    AssemblyContributionModel AssemblyContribution,
    HostModel Host,
    ImmutableArray<HostContextValidationInput> ValidationContexts);
