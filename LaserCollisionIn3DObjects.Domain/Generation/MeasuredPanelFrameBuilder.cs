using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Generation;

/// <summary>Builds the ideal, right-handed panel frame fitted to measured top and left edges.</summary>
public static class MeasuredPanelFrameBuilder
{
    private const float MinimumEdgeLengthSquared = 1e-12f;

    public static MeasuredPanelFrame Create(Vector3 leftTop, Vector3 rightTop, Vector3 leftBottom)
    {
        var widthEdge = rightTop - leftTop;
        var downEdge = leftBottom - leftTop;
        if (!IsFinite(widthEdge) || !IsFinite(downEdge)
            || widthEdge.LengthSquared() <= MinimumEdgeLengthSquared
            || downEdge.LengthSquared() <= MinimumEdgeLengthSquared)
        {
            throw new ArgumentException("Measured panel edges must be finite and non-zero.");
        }

        var width = Vector3.Normalize(widthEdge);
        var downRaw = Vector3.Normalize(downEdge);
        var normalRaw = Vector3.Cross(width, downRaw);
        if (normalRaw.LengthSquared() <= MinimumEdgeLengthSquared)
        {
            throw new ArgumentException("Measured panel width and height edges must not be parallel.");
        }

        var normal = Vector3.Normalize(normalRaw);
        var down = Vector3.Normalize(Vector3.Cross(normal, width));
        if (Vector3.Dot(down, downRaw) < 0f)
        {
            down = -down;
            normal = -normal;
        }

        // System.Numerics stores the transformed local axes in the matrix rows.
        var matrix = new Matrix4x4(
            -normal.X, -normal.Y, -normal.Z, 0f,
            width.X, width.Y, width.Z, 0f,
            -down.X, -down.Y, -down.Z, 0f,
            0f, 0f, 0f, 1f);
        var orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
        return new MeasuredPanelFrame(width, down, normal, orientation);
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public readonly record struct MeasuredPanelFrame(
    Vector3 Width,
    Vector3 Down,
    Vector3 Normal,
    Quaternion Orientation);
