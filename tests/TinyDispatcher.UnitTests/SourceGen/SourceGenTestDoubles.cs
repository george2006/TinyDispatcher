#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Generator;

namespace TinyDispatcher.UnitTests.SourceGen;

internal sealed class CapturingGeneratorContext : IGeneratorContext
{
    public List<(string HintName, string Content)> Sources { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();

    public void AddSource(string hintName, SourceText sourceText)
    {
        Sources.Add((hintName, sourceText.ToString()));
    }

    public void ReportDiagnostic(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }
}

internal sealed class EmptyAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    public static readonly EmptyAnalyzerConfigOptionsProvider Instance = new();

    public override AnalyzerConfigOptions GlobalOptions => EmptyAnalyzerConfigOptions.Instance;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return EmptyAnalyzerConfigOptions.Instance;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return EmptyAnalyzerConfigOptions.Instance;
    }
}

internal sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
{
    public static readonly EmptyAnalyzerConfigOptions Instance = new();

    public override bool TryGetValue(string key, out string value)
    {
        value = string.Empty;
        return false;
    }
}
