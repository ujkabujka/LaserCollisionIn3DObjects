using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Rendering.Helix;

/// <summary>
/// Creates visual guides for object and light-source frames.
/// </summary>
public sealed class HelixFrameVisualizer
{
    public IReadOnlyList<Visual3D> CreateGlobalFrameVisuals(float axisLength = 10f)
    {
        return CreateFrameVisuals(new Frame3D(Vector3.Zero, Quaternion.Identity), axisLength);
    }

    public IReadOnlyList<Visual3D> CreateFrameVisualsBatch(IReadOnlyList<(Frame3D Frame, float AxisLength)> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            return [];
        }

        var xAxisPoints = new Point3DCollection(frames.Count * 2);
        var yAxisPoints = new Point3DCollection(frames.Count * 2);
        var zAxisPoints = new Point3DCollection(frames.Count * 2);

        foreach (var (frame, axisLength) in frames)
        {
            var safeAxisLength = Math.Max(axisLength, 0.5f);
            var origin = frame.Position;

            xAxisPoints.Add(ToPoint3D(origin));
            xAxisPoints.Add(ToPoint3D(origin + frame.TransformDirectionToWorld(Vector3.UnitX * safeAxisLength)));

            yAxisPoints.Add(ToPoint3D(origin));
            yAxisPoints.Add(ToPoint3D(origin + frame.TransformDirectionToWorld(Vector3.UnitY * safeAxisLength)));

            zAxisPoints.Add(ToPoint3D(origin));
            zAxisPoints.Add(ToPoint3D(origin + frame.TransformDirectionToWorld(Vector3.UnitZ * safeAxisLength)));
        }

        return
        [
            new LinesVisual3D { Color = Colors.IndianRed, Thickness = 2, Points = xAxisPoints },
            new LinesVisual3D { Color = Colors.ForestGreen, Thickness = 2, Points = yAxisPoints },
            new LinesVisual3D { Color = Colors.DodgerBlue, Thickness = 2, Points = zAxisPoints },
        ];
    }

    public IReadOnlyList<Visual3D> CreateFrameVisuals(Frame3D frame, float axisLength)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var safeAxisLength = Math.Max(axisLength, 0.5f);
        var origin = frame.Position;

        return
        [
            CreateAxis(origin, origin + frame.TransformDirectionToWorld(Vector3.UnitX * safeAxisLength), Colors.IndianRed),
            CreateAxis(origin, origin + frame.TransformDirectionToWorld(Vector3.UnitY * safeAxisLength), Colors.ForestGreen),
            CreateAxis(origin, origin + frame.TransformDirectionToWorld(Vector3.UnitZ * safeAxisLength), Colors.DodgerBlue),
            new SphereVisual3D
            {
                Center = ToPoint3D(origin),
                Radius = Math.Max(safeAxisLength * 0.05d, 0.08d),
                Fill = new SolidColorBrush(Colors.WhiteSmoke),
                ThetaDiv = 12,
                PhiDiv = 12,
            },
        ];
    }

    private static Visual3D CreateAxis(Vector3 start, Vector3 end, Color color)
    {
        return new LinesVisual3D
        {
            Color = color,
            Thickness = 2,
            Points = new Point3DCollection
            {
                ToPoint3D(start),
                ToPoint3D(end),
            },
        };
    }

    private static Point3D ToPoint3D(Vector3 value)
    {
        return new Point3D(value.X, value.Y, value.Z);
    }
}
