using System.Collections.ObjectModel;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Scene;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using LaserCollisionIn3DObjects.Wpf.Services;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

/// <summary>
/// Represents one editable collision scene in the workspace.
/// </summary>
public sealed class CollisionSceneViewModel : ObservableObject
{
    private string _name;
    private PrismItemViewModel? _selectedPrism;
    private RayItemViewModel? _selectedRay;
    private CylindricalLightSourceItemViewModel? _selectedLightSource;
    private ProjectedLightSourceItemViewModel? _selectedProjectedLightSource;
    private bool _isProjectionOnly;
    private PanelCollisionResult? _selectedPanelResult;
    private PlotModel? _panelPlotModel;
    private CollisionSourceLibraryItemViewModel? _assignedSource;
    private bool _showCollisionRays = true;
    private bool _showCollisionHitPoints = true;
    private int _sceneOrdinal = 1;
    public CollisionSceneViewModel(string name)
    {
        _name = name;
        Prisms.CollectionChanged += (_, _) => InvalidateCollisionResults();
        Rays.CollectionChanged += (_, _) => InvalidateCollisionResults();
        LightSources.CollectionChanged += (_, _) => InvalidateCollisionResults();
        ProjectedLightSources.CollectionChanged += (_, _) => InvalidateCollisionResults();
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool ShowCollisionRays { get => _showCollisionRays; set => SetProperty(ref _showCollisionRays, value); }
    public bool ShowCollisionHitPoints { get => _showCollisionHitPoints; set => SetProperty(ref _showCollisionHitPoints, value); }
    public int SceneOrdinal { get => _sceneOrdinal; internal set => SetProperty(ref _sceneOrdinal, value); }
    internal SceneRenderSyncService.CollisionComputation? LastCollisionComputation { get; set; }

    public ObservableCollection<PrismItemViewModel> Prisms { get; } = new();
    public ObservableCollection<RayItemViewModel> Rays { get; } = new();
    public ObservableCollection<CylindricalLightSourceItemViewModel> LightSources { get; } = new();
    public ObservableCollection<ProjectedLightSourceItemViewModel> ProjectedLightSources { get; } = new();
    public CollisionSourceLibraryItemViewModel? AssignedSource
    {
        get => _assignedSource;
        private set => SetProperty(ref _assignedSource, value);
    }

    /// <summary>Replaces the sole scene source with a deep, scene-owned snapshot.</summary>
    public void AssignSource(CollisionSourceLibraryItemViewModel? definition)
    {
        LightSources.Clear();
        ProjectedLightSources.Clear();
        AssignedSource = definition?.DeepClone();
        if (AssignedSource?.GeneratedSource is { } generated) LightSources.Add(generated);
        if (AssignedSource?.TransferredSource is { } transferred) ProjectedLightSources.Add(transferred);
        SelectedLightSource = AssignedSource?.GeneratedSource;
        SelectedProjectedLightSource = AssignedSource?.TransferredSource;
        InvalidateCollisionResults();
    }
    public ObservableCollection<HitResultItemViewModel> HitResults { get; } = new();
    public IReadOnlyList<CollisionHitPointRecord> HitPointRecords { get; private set; } = Array.Empty<CollisionHitPointRecord>();
    public bool HasValidCollisionRun { get; private set; }
    public PanelCollisionAnalysis? PanelAnalysis { get; private set; }
    public IReadOnlyList<PanelCollisionResult> PanelResults => PanelAnalysis?.Panels ?? Array.Empty<PanelCollisionResult>();
    public PanelCollisionResult? SelectedPanelResult
    {
        get => _selectedPanelResult;
        set
        {
            if (SetProperty(ref _selectedPanelResult, value)) PanelPlotModel = CreatePanelPlot(value);
        }
    }
    public PlotModel? PanelPlotModel { get => _panelPlotModel; private set => SetProperty(ref _panelPlotModel, value); }

    public void PublishCollisionResults(IEnumerable<HitResultItemViewModel> rows, IReadOnlyList<CollisionHitPointRecord> records, PanelCollisionAnalysis? analysis = null)
    {
        HitResults.Clear();
        foreach (var row in rows) HitResults.Add(row);
        HitPointRecords = records;
        PanelAnalysis = analysis;
        SelectedPanelResult = analysis?.Panels.FirstOrDefault(panel => panel.HitCount > 0) ?? analysis?.Panels.FirstOrDefault();
        HasValidCollisionRun = true;
        RaisePropertyChanged(nameof(HitPointRecords));
        RaisePropertyChanged(nameof(HasValidCollisionRun));
        RaisePropertyChanged(nameof(PanelAnalysis));
        RaisePropertyChanged(nameof(PanelResults));
    }

    public void InvalidateCollisionResults()
    {
        HitResults.Clear();
        HitPointRecords = Array.Empty<CollisionHitPointRecord>();
        PanelAnalysis = null;
        SelectedPanelResult = null;
        HasValidCollisionRun = false;
        LastCollisionComputation = null;
        RaisePropertyChanged(nameof(HitPointRecords));
        RaisePropertyChanged(nameof(HasValidCollisionRun));
        RaisePropertyChanged(nameof(PanelAnalysis));
        RaisePropertyChanged(nameof(PanelResults));
    }
    public ObservableCollection<Point3> HolePoints { get; } = new();
    public ObservableCollection<Point3> NaturalPoints { get; } = new();
    public ObservableCollection<Point3> MeasuredCornerPoints { get; } = new();
    public SceneProjectionState ProjectionState { get; } = new();
    public bool HasHolePoints => HolePoints.Count > 0;
    public bool IsProjectionOnly
    {
        get => _isProjectionOnly;
        set => SetProperty(ref _isProjectionOnly, value);
    }

    private static PlotModel? CreatePanelPlot(PanelCollisionResult? panel)
    {
        if (panel is null) return null;
        var model = new PlotModel { Title = $"{panel.PanelName} — {panel.HitCount} hits", PlotType = PlotType.Cartesian, IsLegendVisible = false };
        var halfY = panel.SizeY / 2d;
        var halfZ = panel.SizeZ / 2d;
        var padding = Math.Max(panel.SizeY, panel.SizeZ) * .08;
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Panel Local Y", Minimum = -halfY - padding, Maximum = halfY + padding });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Panel Local Z", Minimum = -halfZ - padding, Maximum = halfZ + padding });
        var outline = new LineSeries { Color = OxyColors.SteelBlue, StrokeThickness = 2 };
        outline.Points.AddRange([new(-halfY, -halfZ), new(halfY, -halfZ), new(halfY, halfZ), new(-halfY, halfZ), new(-halfY, -halfZ)]);
        model.Series.Add(outline);
        var points = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 4, MarkerFill = OxyColors.OrangeRed, TrackerFormatString = "Panel Local Y: {2:0.###}\nPanel Local Z: {4:0.###}\n{Tag}" };
        foreach (var hit in panel.Hits) points.Points.Add(new ScatterPoint(hit.LocalY, hit.LocalZ, tag: $"{hit.SourceName} ({hit.SourceType})"));
        model.Series.Add(points);
        return model;
    }

    public PrismItemViewModel? SelectedPrism
    {
        get => _selectedPrism;
        set => SetProperty(ref _selectedPrism, value);
    }

    public RayItemViewModel? SelectedRay
    {
        get => _selectedRay;
        set => SetProperty(ref _selectedRay, value);
    }

    public CylindricalLightSourceItemViewModel? SelectedLightSource
    {
        get => _selectedLightSource;
        set => SetProperty(ref _selectedLightSource, value);
    }

    public ProjectedLightSourceItemViewModel? SelectedProjectedLightSource
    {
        get => _selectedProjectedLightSource;
        set => SetProperty(ref _selectedProjectedLightSource, value);
    }

}
