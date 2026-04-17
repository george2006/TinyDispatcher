using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorAnalysis(
    Compilation Compilation,
    ImmutableArray<InvocationExpressionSyntax> UseTinyCallsSyntax,
    GeneratorOptions EffectiveOptions,
    GeneratorExtraction Extraction);
