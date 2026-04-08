using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

/// <summary>
/// Represents a rigid transform in 3D using position and orientation.
/// </summary>
public sealed class Frame3D
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Frame3D"/> class.
    /// </summary>
    public Frame3D()
        : this(Vector3.Zero, Quaternion.Identity)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Frame3D"/> class with explicit transform values.
    /// </summary>
    /// <param name="position">World-space position.</param>
    /// <param name="orientation">World-space orientation.</param>
    public Frame3D(Vector3 position, Quaternion orientation)
    {
        Position = position;
        Orientation = Quaternion.Normalize(orientation);
    }

    /// <summary>
    /// Gets or sets frame position in world space.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Gets or sets frame orientation in world space.
    /// </summary>
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Gets the matrix that transforms local-space coordinates to world space.
    /// </summary>
    public Matrix4x4 GetLocalToWorldMatrix()
    {
        var rotation = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Orientation));
        var translation = Matrix4x4.CreateTranslation(Position);
        return rotation * translation;
    }

    /// <summary>
    /// Gets the matrix that transforms world-space coordinates to local space.
    /// </summary>
    public Matrix4x4 GetWorldToLocalMatrix()
    {
        var localToWorld = GetLocalToWorldMatrix();
        Matrix4x4.Invert(localToWorld, out var worldToLocal);
        return worldToLocal;
    }

    /// <summary>
    /// Transforms a point from local space to world space.
    /// </summary>
    /// <param name="localPoint">Point in local space.</param>
    /// <returns>Point in world space.</returns>
    public Vector3 TransformPointToWorld(Vector3 localPoint)
    {
        return Vector3.Transform(localPoint, GetLocalToWorldMatrix());
    }

    /// <summary>
    /// Transforms a point from world space to local space.
    /// </summary>
    /// <param name="worldPoint">Point in world space.</param>
    /// <returns>Point in local space.</returns>
    public Vector3 TransformPointToLocal(Vector3 worldPoint)
    {
        return Vector3.Transform(worldPoint, GetWorldToLocalMatrix());
    }

    /// <summary>
    /// Transforms a direction vector from local space to world space.
    /// </summary>
    /// <param name="localDirection">Direction in local space.</param>
    /// <returns>Direction in world space.</returns>
    public Vector3 TransformDirectionToWorld(Vector3 localDirection)
    {
        return Vector3.TransformNormal(localDirection, Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Orientation)));
    }

    /// <summary>
    /// Transforms a direction vector from world space to local space.
    /// </summary>
    /// <param name="worldDirection">Direction in world space.</param>
    /// <returns>Direction in local space.</returns>
    public Vector3 TransformDirectionToLocal(Vector3 worldDirection)
    {
        var inverseRotation = Quaternion.Inverse(Quaternion.Normalize(Orientation));
        return Vector3.Transform(worldDirection, inverseRotation);
    }
}
