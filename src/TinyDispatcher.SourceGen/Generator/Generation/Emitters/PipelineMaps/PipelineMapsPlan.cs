#nullable enable

using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal sealed record PipelineMapsPlan(
    ImmutableArray<PipelineDescriptor> Descriptors,
    ImmutableArray<PipelineMapAttributeDescriptor> AttributeDescriptors,
    PipelineMapOutputFormats Formats)
{
    public bool ShouldEmit => ShouldEmitDocuments || ShouldEmitAttributes;

    private bool ShouldEmitDocuments =>
        Descriptors.Length > 0 && (Formats.EmitJson || Formats.EmitMermaid);

    private bool ShouldEmitAttributes =>
        AttributeDescriptors.Length > 0 && Formats.EmitAttributes;

    public static PipelineMapsPlan Empty { get; } =
        new(
            ImmutableArray<PipelineDescriptor>.Empty,
            ImmutableArray<PipelineMapAttributeDescriptor>.Empty,
            PipelineMapOutputFormats.DefaultJson());
}

