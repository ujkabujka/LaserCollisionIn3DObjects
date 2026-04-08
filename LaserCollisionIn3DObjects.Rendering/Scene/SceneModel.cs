using LaserCollisionIn3DObjects.Domain.Prisms;
using LaserCollisionIn3DObjects.Domain.Rays;

namespace LaserCollisionIn3DObjects.Rendering.Scene;

public sealed class SceneModel
{
    public IList<Ray> Rays { get; } = new List<Ray>();

    public IList<RectangularPrism> Prisms { get; } = new List<RectangularPrism>();
}
