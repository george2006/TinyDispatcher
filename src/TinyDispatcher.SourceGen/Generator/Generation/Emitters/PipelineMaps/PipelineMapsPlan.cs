#nullable enable

using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal sealed record PipelineMapsPlan(
    ImmutableArray<PipelineDescriptor> Descriptors,
    PipelineMapOutputFormats Formats)
{
    public bool ShouldEmit =>
        Descriptors.Length > 0 &&
        (Formats.EmitJson || Formats.EmitMermaid);

    public static PipelineMapsPlan Empty { get; } =
        new(
            ImmutableArray<PipelineDescriptor>.Empty,
            PipelineMapOutputFormats.DefaultJson());
}

