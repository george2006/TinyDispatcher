#nullable enable

using System;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal readonly struct PipelineMapOutputFormats
{
    public bool EmitJson { get; }

    public bool EmitMermaid { get; }

    public bool EmitAttributes { get; }

    private PipelineMapOutputFormats(bool emitJson, bool emitMermaid, bool emitAttributes)
    {
        EmitJson = emitJson;
        EmitMermaid = emitMermaid;
        EmitAttributes = emitAttributes;
    }

    public static PipelineMapOutputFormats DefaultJson()
        => new(emitJson: true, emitMermaid: false, emitAttributes: false);

    public static PipelineMapOutputFormats ParseOrDefault(string? raw)
    {
        var parsed = Parse(raw);

        if (!parsed.EmitJson && !parsed.EmitMermaid && !parsed.EmitAttributes)
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
        var attributes = false;

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
            else if (part == "attributes")
            {
                attributes = true;
            }
        }

        return new PipelineMapOutputFormats(json, mermaid, attributes);
    }
}

