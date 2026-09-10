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
    ImportedTextFile,
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

    /// <summary>Exact rays for non-projection sources. They intentionally carry no target-hole metadata.</summary>
    public ObservableCollection<Ray3D> ExactRays { get; } = new();

    public int EffectiveRayCount => ExactRays.Count > 0 ? ExactRays.Count : Rays.Count;

    public IEnumerable<Ray3D> GetEffectiveCollisionRays() =>
        ExactRays.Count > 0 ? ExactRays : Rays.Select(ray => ray.Ray);

    public string SourceTypeLabel => OriginKind switch
    {
        ProjectedLightSourceOriginKind.CompletedProjectionResult => "Completed",
        ProjectedLightSourceOriginKind.ImportedTextFile => "Imported",
        _ => "Projected",
    };

    public ProjectedLightSourceItemViewModel()
    {
        Rays.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(EffectiveRayCount));
        ExactRays.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(EffectiveRayCount));
    }

    public NumericsQuaternion BaseOrientation { get; set; } = NumericsQuaternion.Identity;
    public ProjectedLightSourceOriginKind OriginKind { get; set; } = ProjectedLightSourceOriginKind.ProjectionResult;

    public ProjectedLightSourceItemViewModel DeepClone()
    {
        var clone = new ProjectedLightSourceItemViewModel
        {
            Name = Name, ProfileDefinition = ProfileDefinition with { },
            SourceFrame = new PointSourceFrameState { Origin = SourceFrame.Origin, AxisX = SourceFrame.AxisX, AxisY = SourceFrame.AxisY, AxisZ = SourceFrame.AxisZ },
            BaseOrientation = BaseOrientation, OriginKind = OriginKind,
        };
        foreach (var ray in Rays) clone.Rays.Add(ray);
        foreach (var ray in ExactRays) clone.ExactRays.Add(ray);
        return clone;
    }
}
