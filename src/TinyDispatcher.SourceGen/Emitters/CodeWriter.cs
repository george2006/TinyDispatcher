using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Emitters;

internal sealed class CodeWriter
{
    private readonly StringBuilder _sb;
    private int _indent;
    private readonly Stack<string> _blocks = new();

    public CodeWriter(int capacity = 96_000) => _sb = new StringBuilder(capacity);

    public override string ToString() => _sb.ToString();

    public void Line(string text = "")
    {
        if (text.Length == 0)
        {
            _sb.AppendLine();
            return;
        }

        _sb.Append(' ', _indent * 2);
        _sb.AppendLine(text);
    }

    public void BeginBlock(string headerLine)
    {
        Line(headerLine);
        Line("{");
        _blocks.Push(headerLine);
        _indent++;
    }

    public void BeginAnonymousBlock(string labelForDebug = "{")
    {
        Line("{");
        _blocks.Push(labelForDebug);
        _indent++;
    }

    public void EndBlock()
    {
        if (_blocks.Count == 0)
            throw new InvalidOperationException("Attempted to close a block but none are open.");

        _indent--;
        Line("}");
        _blocks.Pop();
    }

    public void EnsureAllBlocksClosed()
    {
        if (_blocks.Count != 0)
            throw new InvalidOperationException("Unclosed block(s). Top block: " + _blocks.Peek());
    }
}

