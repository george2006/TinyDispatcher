using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ConfirmedBootstrapLambda(
    SemanticModel SemanticModel,
    LambdaExpressionSyntax Lambda);
