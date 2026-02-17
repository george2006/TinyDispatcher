using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = new();

    public int Count => _items.Count;
    public bool HasErrors => _items.Any(d => d.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic)
    {
        if (diagnostic is null) return;
        _items.Add(diagnostic);
    }

    public void AddRange(IEnumerable<Diagnostic> diags)
    {
        if (diags is null) return;
        foreach (var d in diags) Add(d);
    }

    public ImmutableArray<Diagnostic> ToImmutable() => _items.ToImmutableArray();
}
