using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal static class Fqn
{
    public static string EnsureGlobal(string s)
        => s.StartsWith("global::", StringComparison.Ordinal) ? s : "global::" + s;

    public static string FromType(ITypeSymbol type)
        => EnsureGlobal(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}
