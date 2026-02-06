using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal readonly struct OrderKey
{
    public readonly string FilePath;
    public readonly int SpanStart;

    public OrderKey(string filePath, int spanStart)
        => (FilePath, SpanStart) = (filePath, spanStart);

    public static OrderKey From(SyntaxNode node)
    {
        var tree = node.SyntaxTree;
        var path = tree != null ? (tree.FilePath ?? string.Empty) : string.Empty;
        return new OrderKey(path, node.SpanStart);
    }
}

