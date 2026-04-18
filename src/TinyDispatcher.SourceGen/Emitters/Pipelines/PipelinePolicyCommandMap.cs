#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelinePolicyCommandMap
{
    public static void AddFirstPolicyWins<TValue>(
        Dictionary<string, TValue> map,
        ImmutableArray<string> commands,
        TValue value)
    {
        for (var commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            var command = PipelineTypeNames.NormalizeFqn(commands[commandIndex]);
            var commandIsMissing = string.IsNullOrWhiteSpace(command);

            if (commandIsMissing)
            {
                continue;
            }

            var commandAlreadyHasPolicy = map.ContainsKey(command);

            if (commandAlreadyHasPolicy)
            {
                continue;
            }

            map[command] = value;
        }
    }
}
