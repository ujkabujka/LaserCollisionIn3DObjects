using System.Collections.ObjectModel;
using NumericsQuaternion = System.Numerics.Quaternion;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public enum ProjectedLightSourceOriginKind
{
    ProjectionResult,
    CompletedProjectionResult,
}

public sealed class ProjectedLightSourceItemViewModel : ObservableObject
{
    private string _name = "Projected Source";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public AxisymmetricSourceProfileDefinition ProfileDefinition { get; set; } = new();

    public PointSourceFrameState SourceFrame { get; set; } = new()
    {
        Origin = new Point3(0, 0, 0),
        AxisX = new Vector3D(1, 0, 0),
        AxisY = new Vector3D(0, 1, 0),
        AxisZ = new Vector3D(0, 0, 1),
    };

    public ObservableCollection<ProjectionRay> Rays { get; } = new();

    public NumericsQuaternion BaseOrientation { get; set; } = NumericsQuaternion.Identity;
    public ProjectedLightSourceOriginKind OriginKind { get; set; } = ProjectedLightSourceOriginKind.ProjectionResult;
}
