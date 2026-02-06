namespace TinyDispatcher.SourceGen.Generator.Models;

internal static class Fqn
{
    public static string EnsureGlobal(string s)
        => s.StartsWith("global::", StringComparison.Ordinal) ? s : "global::" + s;
}