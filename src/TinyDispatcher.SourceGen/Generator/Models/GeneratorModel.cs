namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorModel(
    AssemblyContributionModel AssemblyContribution,
    ReferencedAssemblyContributions References,
    HostModel Host);
