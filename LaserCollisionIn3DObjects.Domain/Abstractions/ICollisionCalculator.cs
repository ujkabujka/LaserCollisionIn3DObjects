using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Prisms;
using LaserCollisionIn3DObjects.Domain.Rays;

namespace LaserCollisionIn3DObjects.Domain.Abstractions;

public interface ICollisionCalculator
{
    Point3? GetFirstCollision(Ray ray, IReadOnlyCollection<RectangularPrism> prisms);
}
