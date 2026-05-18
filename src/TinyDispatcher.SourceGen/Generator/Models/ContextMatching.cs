namespace TinyDispatcher.SourceGen.Generator.Models;

internal static class ContextMatching
{
    public static bool Matches(string? contributionContextFqn, string hostContextFqn)
    {
        if (string.IsNullOrWhiteSpace(contributionContextFqn) ||
            string.IsNullOrWhiteSpace(hostContextFqn))
        {
            return true;
        }

        return string.Equals(
            contributionContextFqn,
            hostContextFqn,
            System.StringComparison.Ordinal);
    }
}
