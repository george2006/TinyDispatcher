using global::TinyDispatcher.SourceGen.Emitters;
using System;
using Xunit;

namespace TinyDispatcher.UnitTests.PipelineEmitter;

public sealed class CodeWriterTests
{
    [Fact]
    public void End_block_without_begin_throws()
    {
        var w = new PipelineEmitterRefactored.CodeWriter();

        var ex = Assert.Throws<InvalidOperationException>(() => w.EndBlock());

        Assert.Contains("Attempted to close a block", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_all_blocks_closed_throws_when_unclosed()
    {
        var w = new PipelineEmitterRefactored.CodeWriter();

        w.BeginBlock("namespace X");

        var ex = Assert.Throws<InvalidOperationException>(() => w.EnsureAllBlocksClosed());

        Assert.Contains("Unclosed block", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("namespace X", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Blocks_are_closed_and_braces_match()
    {
        var w = new PipelineEmitterRefactored.CodeWriter();

        w.BeginBlock("namespace X");
        w.BeginBlock("internal sealed class A");
        w.BeginBlock("public void M()");
        w.Line("return;");
        w.EndBlock(); // M
        w.EndBlock(); // A
        w.EndBlock(); // namespace

        w.EnsureAllBlocksClosed();

        var text = w.ToString();

        Assert.Equal(Count(text, "{"), Count(text, "}"));
    }

    private static int Count(string text, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}

