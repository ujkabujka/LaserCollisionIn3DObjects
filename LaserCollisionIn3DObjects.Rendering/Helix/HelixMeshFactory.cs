using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Rendering.Helix;

/// <summary>
/// Creates Helix/WPF mesh visuals for domain geometry.
/// </summary>
public sealed class HelixMeshFactory
{
    public ModelVisual3D CreateRectangularPrismBatch(IReadOnlyList<RectangularPrism> prisms, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(prisms);

        var material = MaterialHelper.CreateMaterial(color ?? Colors.SteelBlue);
        var group = new Model3DGroup();

        foreach (var prism in prisms)
        {
            var meshBuilder = new MeshBuilder();
            meshBuilder.AddBox(new Point3D(0, 0, 0), prism.SizeX, prism.SizeY, prism.SizeZ);
            var mesh = meshBuilder.ToMesh();

            group.Children.Add(new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material,
                Transform = CreateTransform(prism.Frame.Position, prism.Frame.Orientation),
            });
        }

        return new ModelVisual3D { Content = group };
    }

    /// <summary>
    /// Creates a prism visual from a <see cref="RectangularPrism"/> domain object.
    /// </summary>
    /// <param name="prism">Domain prism definition.</param>
    /// <param name="color">Optional diffuse color.</param>
    /// <returns>A model visual for insertion into a Helix viewport.</returns>
    public ModelVisual3D CreateRectangularPrism(RectangularPrism prism, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(prism);

        var meshBuilder = new MeshBuilder();
        meshBuilder.AddBox(new Point3D(0, 0, 0), prism.SizeX, prism.SizeY, prism.SizeZ);

        var mesh = meshBuilder.ToMesh();
        var material = MaterialHelper.CreateMaterial(color ?? Colors.SteelBlue);

        var geometry = new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material,
            Transform = CreateTransform(prism.Frame.Position, prism.Frame.Orientation),
        };

        return new ModelVisual3D { Content = geometry };
    }

    /// <summary>
    /// Creates a cylinder visual from a <see cref="CylindricalLightSource"/> domain object.
    /// </summary>
    public ModelVisual3D CreateCylindricalLightSource(CylindricalLightSource source, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var halfHeight = source.Height * 0.5f;
        var meshBuilder = new MeshBuilder();
        meshBuilder.AddCylinder(
            new Point3D(0, -halfHeight, 0),
            new Point3D(0, halfHeight, 0),
            source.Radius,
            32);

        var mesh = meshBuilder.ToMesh();
        var material = MaterialHelper.CreateMaterial(color ?? Colors.Goldenrod);

        var geometry = new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material,
            Transform = CreateTransform(source.Frame.Position, source.Frame.Orientation),
        };

        return new ModelVisual3D { Content = geometry };
    }

    public ModelVisual3D CreateCylindricalLightSourceBatch(IReadOnlyList<CylindricalLightSource> sources, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var material = MaterialHelper.CreateMaterial(color ?? Colors.Goldenrod);
        var group = new Model3DGroup();

        foreach (var source in sources)
        {
            //var halfHeight = source.Height * 0.5f;
            var meshBuilder = new MeshBuilder();
            meshBuilder.AddCylinder(
                new Point3D(0, 0, 0),
                new Point3D(source.Height, 0, 0),
                source.Radius,
                32);

            group.Children.Add(new GeometryModel3D
            {
                Geometry = meshBuilder.ToMesh(),
                Material = material,
                BackMaterial = material,
                Transform = CreateTransform(source.Frame.Position, source.Frame.Orientation),
            });
        }

        return new ModelVisual3D { Content = group };
    }


    public ModelVisual3D CreateAxisymmetricLightSourceBatch(IReadOnlyList<AxisymmetricLightSource> sources, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var material = MaterialHelper.CreateMaterial(color ?? Colors.Goldenrod);
        var group = new Model3DGroup();

        foreach (var source in sources)
        {
            var meshBuilder = new MeshBuilder(generateNormals: true, generateTexCoords: false);
            const int slices = 32;
            const int stacks = 20;

            for (var stack = 0; stack < stacks; stack++)
            {
                var u0 = source.Profile.Length * (stack / (float)stacks);
                var u1 = source.Profile.Length * ((stack + 1) / (float)stacks);

                for (var slice = 0; slice < slices; slice++)
                {
                    var t0 = 2f * MathF.PI * (slice / (float)slices);
                    var t1 = 2f * MathF.PI * ((slice + 1) / (float)slices);

                    var p00 = ToPoint3D(source.Profile.EvaluateSurfacePoint(u0, t0));
                    var p01 = ToPoint3D(source.Profile.EvaluateSurfacePoint(u0, t1));
                    var p10 = ToPoint3D(source.Profile.EvaluateSurfacePoint(u1, t0));
                    var p11 = ToPoint3D(source.Profile.EvaluateSurfacePoint(u1, t1));
                    meshBuilder.AddQuad(p00, p01, p11, p10);
                }
            }

            group.Children.Add(new GeometryModel3D
            {
                Geometry = meshBuilder.ToMesh(),
                Material = material,
                BackMaterial = material,
                Transform = CreateTransform(source.Frame.Position, source.Frame.Orientation),
            });
        }

        return new ModelVisual3D { Content = group };
    }

    private static Point3D ToPoint3D(Vector3 point) => new(point.X, point.Y, point.Z);
    private static Transform3D CreateTransform(Vector3 position, System.Numerics.Quaternion orientation)
    {
        var rotation = new RotateTransform3D(
            new QuaternionRotation3D(new System.Windows.Media.Media3D.Quaternion(
                orientation.X,
                orientation.Y,
                orientation.Z,
                orientation.W)));

        var translation = new TranslateTransform3D(position.X, position.Y, position.Z);

        var transformGroup = new Transform3DGroup();
        transformGroup.Children.Add(rotation);
        transformGroup.Children.Add(translation);
        return transformGroup;
    }
}
