namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorExtraction(
    ThisAssemblyExtraction ThisAssembly,
    ReferencedAssemblyContributions ReferencedContributions);
