using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorAnalysisResult(
    GeneratorAnalysis Analysis,
    ImmutableArray<InvocationExpressionSyntax> ConfirmedUseTinyCallsSyntax);
