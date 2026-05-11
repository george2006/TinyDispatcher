#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator;

namespace TinyDispatcher.UnitTests.SourceGen;

internal static class SourceGenCompilationReferences
{
    public static List<MetadataReference> CurrentDomainWithoutUnitTests()
    {
        var unitTestAssemblyName = typeof(SourceGenCompilationReferences).Assembly.GetName().Name;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly =>
                !assembly.IsDynamic &&
                !string.IsNullOrWhiteSpace(assembly.Location) &&
                !string.Equals(
                    assembly.GetName().Name,
                    unitTestAssemblyName,
                    StringComparison.Ordinal))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return references;
    }
}
