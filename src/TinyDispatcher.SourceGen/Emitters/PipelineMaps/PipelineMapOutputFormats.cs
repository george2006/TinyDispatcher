#nullable enable

using System;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal readonly struct PipelineMapOutputFormats
{
    public bool EmitJson { get; }

    public bool EmitMermaid { get; }

    private PipelineMapOutputFormats(bool emitJson, bool emitMermaid)
    {
        EmitJson = emitJson;
        EmitMermaid = emitMermaid;
    }

    public static PipelineMapOutputFormats DefaultJson()
        => new(emitJson: true, emitMermaid: false);

    public static PipelineMapOutputFormats ParseOrDefault(string? raw)
    {
        var parsed = Parse(raw);

        if (!parsed.EmitJson && !parsed.EmitMermaid)
        {
            return DefaultJson();
        }

        return parsed;
    }

    private static PipelineMapOutputFormats Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultJson();
        }

        var parts = raw!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        var json = false;
        var mermaid = false;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim().ToLowerInvariant();

            if (part == "json")
            {
                json = true;
            }
            else if (part == "mermaid")
            {
                mermaid = true;
            }
        }

        return new PipelineMapOutputFormats(json, mermaid);
    }
}
