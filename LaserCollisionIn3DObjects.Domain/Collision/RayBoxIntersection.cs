using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Collision;

/// <summary>
/// Provides slab-method intersection tests between a ray and an axis-aligned box.
/// </summary>
internal static class RayBoxIntersection
{
    /// <summary>
    /// Tests intersection between a ray and an axis-aligned box in the same coordinate space.
    /// </summary>
    public static bool TryIntersect(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 boxMin,
        Vector3 boxMax,
        float parallelEpsilon,
        float distanceEpsilon,
        out float hitDistance,
        out Vector3 hitNormal)
    {
        var tEnter = float.NegativeInfinity;
        var tExit = float.PositiveInfinity;
        var enterNormal = Vector3.Zero;
        var exitNormal = Vector3.Zero;

        // Slab algorithm:
        // For each axis, compute the parameter interval [t1, t2] where the ray is within that axis-aligned slab.
        // The intersection over all axes is [tEnter, tExit]. A hit exists when the interval is non-empty.
        for (var axis = 0; axis < 3; axis++)
        {
            var originComponent = GetComponent(rayOrigin, axis);
            var directionComponent = GetComponent(rayDirection, axis);
            var minComponent = GetComponent(boxMin, axis);
            var maxComponent = GetComponent(boxMax, axis);

            if (MathF.Abs(directionComponent) < parallelEpsilon)
            {
                // Ray is parallel to this pair of slab planes: it must already be inside this slab.
                if (originComponent < minComponent || originComponent > maxComponent)
                {
                    hitDistance = 0f;
                    hitNormal = Vector3.Zero;
                    return false;
                }

                continue;
            }

            var inverseDirection = 1f / directionComponent;
            var t1 = (minComponent - originComponent) * inverseDirection;
            var t2 = (maxComponent - originComponent) * inverseDirection;
            var n1 = GetAxisNormal(axis, -1f);
            var n2 = GetAxisNormal(axis, 1f);

            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                (n1, n2) = (n2, n1);
            }

            if (t1 > tEnter)
            {
                tEnter = t1;
                enterNormal = n1;
            }

            if (t2 < tExit)
            {
                tExit = t2;
                exitNormal = n2;
            }

            if (tEnter > tExit)
            {
                hitDistance = 0f;
                hitNormal = Vector3.Zero;
                return false;
            }
        }

        if (tEnter >= distanceEpsilon)
        {
            hitDistance = tEnter;
            hitNormal = enterNormal;
            return true;
        }

        if (tExit >= distanceEpsilon)
        {
            // Ray origin is inside the box (or very near boundary): first valid hit is the exit point.
            hitDistance = tExit;
            hitNormal = exitNormal;
            return true;
        }

        hitDistance = 0f;
        hitNormal = Vector3.Zero;
        return false;
    }

    private static float GetComponent(Vector3 value, int axis)
    {
        return axis switch
        {
            0 => value.X,
            1 => value.Y,
            _ => value.Z,
        };
    }

    private static Vector3 GetAxisNormal(int axis, float sign)
    {
        return axis switch
        {
            0 => new Vector3(sign, 0f, 0f),
            1 => new Vector3(0f, sign, 0f),
            _ => new Vector3(0f, 0f, sign),
        };
    }
}
