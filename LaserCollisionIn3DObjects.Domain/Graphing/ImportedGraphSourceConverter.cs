using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

/// <summary>Converts the portable source-local transfer representation into physical world-space graph data.</summary>
public sealed class ImportedGraphSourceConverter
{
    public GraphableSourceData Convert(string stableId, LightSourceTransferData source, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(source);

        var frame = source.Frame;
        return new GraphableSourceData
        {
            Id = stableId,
            DisplayName = displayName ?? $"Imported / {source.Name}",
            Kind = GraphableSourceKind.ImportedLightSource,
            AxisX = frame.TransformDirectionToWorld(Vector3.UnitX),
            AxisY = frame.TransformDirectionToWorld(Vector3.UnitY),
            AxisZ = frame.TransformDirectionToWorld(Vector3.UnitZ),
            FrameOrigin = frame.Position,
            SourceLength = source.Profile.Length > 0 ? source.Profile.Length : source.Profile.Height,
            Rays = source.Rays.Select(ray => new Ray3D(
                frame.TransformPointToWorld(ray.PositionLocal),
                frame.TransformDirectionToWorld(ray.DirectionLocal))).ToList(),
        };
    }
}
