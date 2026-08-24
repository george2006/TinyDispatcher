#nullable enable

using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal static class SymbolLocations
{
    public static Location GetPrimary(INamedTypeSymbol type)
        => type.Locations.Length > 0 ? type.Locations[0] : Location.None;
}
