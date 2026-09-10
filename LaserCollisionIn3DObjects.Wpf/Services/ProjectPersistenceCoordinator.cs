using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Persistence;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Scene;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using DomainRay3D = LaserCollisionIn3DObjects.Domain.Geometry.Ray3D;

namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class ProjectPersistenceCoordinator
{
    private readonly JsonStateFileService _jsonService = new();

    public void SaveProject(
        string filePath,
        SceneCollectionService sceneCollectionService,
        CollisionSceneViewModel? selectedCollisionScene,
        AnnotationWorkspaceViewModel annotationWorkspace,
        ProjectionWorkspaceViewModel projectionWorkspace)
    {
        var state = new ProjectState
        {
            Scenes = sceneCollectionService.Scenes.Select(MapScene).ToList(),
            CollisionWorkspace = new CollisionWorkspaceState { SelectedSceneName = selectedCollisionScene?.Name },
            ProjectionWorkspace = projectionWorkspace.ExportWorkspaceState(),
            AnnotationWorkspace = annotationWorkspace.ExportWorkspaceState(),
            AvailableSources = sceneCollectionService.AvailableSources.Select(MapCollisionSource).ToList(),
        };

        _jsonService.SaveProject(filePath, state);
    }

    public void LoadProject(
        string filePath,
        SceneCollectionService sceneCollectionService,
        AnnotationWorkspaceViewModel annotationWorkspace,
        ProjectionWorkspaceViewModel projectionWorkspace)
    {
        var state = _jsonService.LoadProject(filePath);

        sceneCollectionService.Scenes.Clear();
        RestoreLibrary(state, sceneCollectionService);
        foreach (var sceneState in state.Scenes)
        {
            sceneCollectionService.AddScene(MapScene(sceneState, sceneCollectionService), selectScene: false);
        }

        sceneCollectionService.SelectedScene = sceneCollectionService.Scenes
            .FirstOrDefault(scene => scene.Name == state.CollisionWorkspace.SelectedSceneName)
            ?? sceneCollectionService.Scenes.FirstOrDefault();

        annotationWorkspace.ApplyWorkspaceState(state.AnnotationWorkspace);
        projectionWorkspace.ApplyWorkspaceState(state.ProjectionWorkspace);
    }

    public void SaveCollisionTab(string filePath, SceneCollectionService sceneCollectionService, CollisionSceneViewModel? selectedScene)
    {
        var state = new ProjectState
        {
            Scenes = sceneCollectionService.Scenes.Select(MapScene).ToList(),
            CollisionWorkspace = new CollisionWorkspaceState { SelectedSceneName = selectedScene?.Name },
            AvailableSources = sceneCollectionService.AvailableSources.Select(MapCollisionSource).ToList(),
        };

        _jsonService.SaveProject(filePath, state);
    }

    public void LoadCollisionTab(string filePath, SceneCollectionService sceneCollectionService)
    {
        var state = _jsonService.LoadProject(filePath);
        sceneCollectionService.Scenes.Clear();
        RestoreLibrary(state, sceneCollectionService);
        foreach (var sceneState in state.Scenes)
        {
            sceneCollectionService.AddScene(MapScene(sceneState, sceneCollectionService), selectScene: false);
        }

        sceneCollectionService.SelectedScene = sceneCollectionService.Scenes
            .FirstOrDefault(scene => scene.Name == state.CollisionWorkspace.SelectedSceneName)
            ?? sceneCollectionService.Scenes.FirstOrDefault();
    }

    public void SaveProjectionTab(string filePath, SceneCollectionService sceneCollectionService, ProjectionWorkspaceViewModel projectionWorkspace)
    {
        var state = new ProjectState
        {
            Scenes = sceneCollectionService.Scenes.Select(MapScene).ToList(),
            ProjectionWorkspace = projectionWorkspace.ExportWorkspaceState(),
        };

        _jsonService.SaveProject(filePath, state);
    }

    public void LoadProjectionTab(string filePath, SceneCollectionService sceneCollectionService, ProjectionWorkspaceViewModel projectionWorkspace)
    {
        var state = _jsonService.LoadProject(filePath);
        sceneCollectionService.Scenes.Clear();
        foreach (var sceneState in state.Scenes)
        {
            sceneCollectionService.AddScene(MapScene(sceneState, sceneCollectionService), selectScene: false);
        }

        projectionWorkspace.ApplyWorkspaceState(state.ProjectionWorkspace);
    }

    public void SaveAnnotationTab(string filePath, AnnotationWorkspaceViewModel annotationWorkspace)
    {
        var state = new ProjectState
        {
            AnnotationWorkspace = annotationWorkspace.ExportWorkspaceState(),
        };

        _jsonService.SaveProject(filePath, state);
    }

    public void LoadAnnotationTab(string filePath, AnnotationWorkspaceViewModel annotationWorkspace)
    {
        var state = _jsonService.LoadProject(filePath);
        annotationWorkspace.ApplyWorkspaceState(state.AnnotationWorkspace);
    }

    private static SceneState MapScene(CollisionSceneViewModel scene)
    {
        return new SceneState
        {
            Name = scene.Name,
            IsProjectionOnly = scene.IsProjectionOnly,
            Prisms = scene.Prisms.Select(prism => new PrismState
            {
                Name = prism.Name,
                PositionX = prism.PositionX,
                PositionY = prism.PositionY,
                PositionZ = prism.PositionZ,
                RotationX = prism.RotationX,
                RotationY = prism.RotationY,
                RotationZ = prism.RotationZ,
                SizeX = prism.SizeX,
                SizeY = prism.SizeY,
                SizeZ = prism.SizeZ,
                BaseOrientationX = prism.BaseOrientation.X,
                BaseOrientationY = prism.BaseOrientation.Y,
                BaseOrientationZ = prism.BaseOrientation.Z,
                BaseOrientationW = prism.BaseOrientation.W,
            }).ToList(),
            AssignedSource = scene.AssignedSource is null ? null : MapCollisionSource(scene.AssignedSource),
            HolePoints = scene.HolePoints.ToList(),
            NaturalPoints = scene.NaturalPoints.ToList(),
            MeasuredCornerPoints = scene.MeasuredCornerPoints.ToList(),
            Projection = new SceneProjectionStateDto
            {
                SelectedMethodId = scene.ProjectionState.SelectedMethodId,
                SelectedResultKey = scene.ProjectionState.SelectedResultKey,
                Results = scene.ProjectionState.SavedResults.Select(MapProjectionResult).ToList(),
            },
        };
    }

    private static CollisionSourceState MapCollisionSource(CollisionSourceLibraryItemViewModel source) => new()
    {
        SourceId = source.SourceId,
        GeneratedSource = source.GeneratedSource is null ? null : MapGeneratedLightSource(source.GeneratedSource),
        TransferredSource = source.TransferredSource is null ? null : MapProjectedLightSource(source.TransferredSource),
    };

    private static CollisionSourceLibraryItemViewModel MapCollisionSource(CollisionSourceState source) => new()
    {
        SourceId = source.SourceId == Guid.Empty ? Guid.NewGuid() : source.SourceId,
        GeneratedSource = source.GeneratedSource is null ? null : MapGeneratedLightSource(source.GeneratedSource),
        TransferredSource = source.TransferredSource is null ? null : MapProjectedLightSource(source.TransferredSource),
    };

    private static void RestoreLibrary(ProjectState state, SceneCollectionService service)
    {
        service.AvailableSources.Clear();
        foreach (var source in state.AvailableSources) service.AddToLibrary(MapCollisionSource(source));
    }

    private static AxisymmetricLightSourceState MapGeneratedLightSource(CylindricalLightSourceItemViewModel source)
    {
        return new AxisymmetricLightSourceState
        {
            Name = source.Name,
            SourceKind = source.SourceKind,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            RotationX = source.RotationX,
            RotationY = source.RotationY,
            RotationZ = source.RotationZ,
            Radius = source.Radius,
            Height = source.Height,
            RadiusStart = source.RadiusStart,
            RadiusEnd = source.RadiusEnd,
            Length = source.Length,
            ArcRadius = source.ArcRadius,
            OgiveCurvatureDirection = source.OgiveCurvatureDirection,
            RayCount = source.RayCount,
            TiltWeight = source.TiltWeight,
            TiltPointX = source.TiltPointX,
            TiltPointY = source.TiltPointY,
            TiltPointZ = source.TiltPointZ,
            BaseOrientationX = source.BaseOrientation.X,
            BaseOrientationY = source.BaseOrientation.Y,
            BaseOrientationZ = source.BaseOrientation.Z,
            BaseOrientationW = source.BaseOrientation.W,
            Segments = source.HybridSegments.Select(segment => new AxisymmetricSourceSegmentStateDto
            {
                SegmentKind = segment.SegmentKind,
                Length = segment.Length,
                RadiusStart = segment.RadiusStart,
                RadiusEnd = segment.RadiusEnd,
                ArcRadius = segment.IsOgive ? segment.ArcRadius : null,
                OgiveCurvatureDirection = segment.OgiveCurvatureDirection,
            }).ToList(),
        };
    }

    private static ProjectedLightSourceState MapProjectedLightSource(ProjectedLightSourceItemViewModel source)
    {
        return new ProjectedLightSourceState
        {
            Name = source.Name,
            SourceFrame = new PointSourceFrameStateDto
            {
                Origin = source.SourceFrame.Origin,
                AxisX = source.SourceFrame.AxisX,
                AxisY = source.SourceFrame.AxisY,
                AxisZ = source.SourceFrame.AxisZ,
            },
            ProfileDefinition = source.ProfileDefinition,
            Rays = source.Rays.Select(MapProjectionRay).ToList(),
            ExactRays = source.ExactRays.Select(ray => new RayState { OriginX = ray.Origin.X, OriginY = ray.Origin.Y, OriginZ = ray.Origin.Z, DirectionX = ray.Direction.X, DirectionY = ray.Direction.Y, DirectionZ = ray.Direction.Z }).ToList(),
            BaseOrientationX = source.BaseOrientation.X,
            BaseOrientationY = source.BaseOrientation.Y,
            BaseOrientationZ = source.BaseOrientation.Z,
            BaseOrientationW = source.BaseOrientation.W,
            OriginKind = source.OriginKind.ToString(),
        };
    }

    private static ProjectionRayStateDto MapProjectionRay(ProjectionRay ray)
    {
        return new ProjectionRayStateDto
        {
            Ray = new RayState
            {
                OriginX = ray.Ray.Origin.X,
                OriginY = ray.Ray.Origin.Y,
                OriginZ = ray.Ray.Origin.Z,
                DirectionX = ray.Ray.Direction.X,
                DirectionY = ray.Ray.Direction.Y,
                DirectionZ = ray.Ray.Direction.Z,
            },
            TargetHolePoint = ray.TargetHolePoint,
        };
    }

    private static ProjectionRay MapProjectionRay(ProjectionRayStateDto state)
    {
        return new ProjectionRay(
            new DomainRay3D(
                new Vector3(state.Ray.OriginX, state.Ray.OriginY, state.Ray.OriginZ),
                new Vector3(state.Ray.DirectionX, state.Ray.DirectionY, state.Ray.DirectionZ)),
            state.TargetHolePoint);
    }

    private static ProjectionResultStateDto MapProjectionResult(NamedProjectionResultState namedResult)
    {
        return new ProjectionResultStateDto
        {
            Key = namedResult.Key,
            Name = namedResult.DisplayName,
                        PointSourceOrigin = namedResult.Result.PointSourceOrigin,
            SourceFrame = new PointSourceFrameStateDto
            {
                Origin = namedResult.Result.SourceFrame.Origin,
                AxisX = namedResult.Result.SourceFrame.AxisX,
                AxisY = namedResult.Result.SourceFrame.AxisY,
                AxisZ = namedResult.Result.SourceFrame.AxisZ,
            },
            Rays = namedResult.Result.Rays.Select(MapProjectionRay).ToList(),
            AxisymmetricSource = namedResult.Result.AxisymmetricSource is null ? null : new AxisymmetricProjectionStateDto
            {
                                SourceFrame = new PointSourceFrameStateDto
                {
                    Origin = namedResult.Result.AxisymmetricSource.SourceFrame.Origin,
                    AxisX = namedResult.Result.AxisymmetricSource.SourceFrame.AxisX,
                    AxisY = namedResult.Result.AxisymmetricSource.SourceFrame.AxisY,
                    AxisZ = namedResult.Result.AxisymmetricSource.SourceFrame.AxisZ,
                },
                ProfileDefinition = namedResult.Result.AxisymmetricSource.ProfileDefinition,
                LocalTiltPoint = namedResult.Result.AxisymmetricSource.LocalTiltPoint,
                EstimatedTiltWeight = namedResult.Result.AxisymmetricSource.EstimatedTiltWeight,
                Diagnostics = namedResult.Result.AxisymmetricSource.Diagnostics is null ? null : new SelfCalibratingAxisymmetricProjectionDiagnosticsDto
                {
                    RegularityWeight = namedResult.Result.AxisymmetricSource.Diagnostics.RegularityWeight,
                    CandidateScores = namedResult.Result.AxisymmetricSource.Diagnostics.CandidateScores.Select(candidate => new SelfCalibratingAxisymmetricCandidateDiagnosticsDto
                    {
                        Lambda = candidate.Lambda,
                        MeanFitError = candidate.MeanFitError,
                        RegularityError = candidate.RegularityError,
                        Score = candidate.Score,
                    }).ToList(),
                },
                LeastSquaresDiagnostics = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics is null ? null : new LeastSquaresAxisymmetricAlignmentDiagnosticsDto
                {
                    InitialLambda = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.InitialLambda,
                    RefinedLambda = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.RefinedLambda,
                    InitialMeanAlignmentError = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.InitialMeanAlignmentError,
                    FinalMeanAlignmentError = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.FinalMeanAlignmentError,
                    FinalRmsAlignmentError = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.FinalRmsAlignmentError,
                    FinalMeanAngularErrorDegrees = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.FinalMeanAngularErrorDegrees,
                    FinalMaxAngularErrorDegrees = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.FinalMaxAngularErrorDegrees,
                    MaxAngularErrorHoleIndex = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.MaxAngularErrorHoleIndex,
                    Iterations = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.Iterations,
                    Converged = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.Converged,
                    UsesRegularization = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.UsesRegularization,
                    IterationHistory = namedResult.Result.AxisymmetricSource.LeastSquaresDiagnostics.IterationHistory.Select(iteration => new AxisymmetricLeastSquaresIterationDiagnosticsDto
                    {
                        Iteration = iteration.Iteration,
                        ObjectiveError = iteration.ObjectiveError,
                        Lambda = iteration.Lambda,
                        MeanAlignmentError = iteration.MeanAlignmentError,
                        RmsAlignmentError = iteration.RmsAlignmentError,
                        MeanAngularErrorDegrees = iteration.MeanAngularErrorDegrees,
                        MaxAngularErrorDegrees = iteration.MaxAngularErrorDegrees,
                        Improved = iteration.Improved,
                    }).ToList(),
                },
                Points = namedResult.Result.AxisymmetricSource.Points.Select(point => new AxisymmetricProjectionPointStateDto
                {
                    HolePoint = point.HolePoint,
                    SourceSurfacePoint = point.SourceSurfacePoint,
                    RayOrigin = point.RayOrigin,
                    RayDirection = point.RayDirection,
                    ModeledRayDirection = point.ModeledRayDirection,
                    LocalU = point.LocalU,
                    LocalTheta = point.LocalTheta,
                    UnwrappedU = point.UnwrappedU,
                    UnwrappedV = point.UnwrappedV,
                    FitError = point.FitError,
                    AlignmentError = point.AlignmentError,
                    AngularErrorDegrees = point.AngularErrorDegrees,
                }).ToList(),
            },
        };
    }

    private static CollisionSceneViewModel MapScene(SceneState sceneState, SceneCollectionService service)
    {
        var scene = new CollisionSceneViewModel(sceneState.Name);
        scene.IsProjectionOnly = sceneState.IsProjectionOnly;

        foreach (var prism in sceneState.Prisms)
        {
            scene.Prisms.Add(new PrismItemViewModel
            {
                Name = prism.Name,
                PositionX = prism.PositionX,
                PositionY = prism.PositionY,
                PositionZ = prism.PositionZ,
                RotationX = prism.RotationX,
                RotationY = prism.RotationY,
                RotationZ = prism.RotationZ,
                SizeX = prism.SizeX,
                SizeY = prism.SizeY,
                SizeZ = prism.SizeZ,
                BaseOrientation = BaseOrientationPersistence.FromComponents(
                    prism.BaseOrientationX,
                    prism.BaseOrientationY,
                    prism.BaseOrientationZ,
                    prism.BaseOrientationW),
            });
        }

        // Manual rays are intentionally ignored during migration. Legacy source arrays are
        // retained in the reusable catalog, while only the first source is assigned.
        var legacySources = new List<CollisionSourceLibraryItemViewModel>();
        if (sceneState.LightSources.Count > 0)
            legacySources.AddRange(sceneState.LightSources.Select(source => new CollisionSourceLibraryItemViewModel { GeneratedSource = MapGeneratedLightSource(source) }));
        else
            legacySources.AddRange(sceneState.CylindricalLightSources.Select(source => new CollisionSourceLibraryItemViewModel { GeneratedSource = MapGeneratedLightSource(new AxisymmetricLightSourceState
            {
                Name = source.Name, SourceKind = AxisymmetricSourceKind.Cylinder, PositionX = source.PositionX, PositionY = source.PositionY, PositionZ = source.PositionZ,
                RotationX = source.RotationX, RotationY = source.RotationY, RotationZ = source.RotationZ, Radius = source.Radius, Height = source.Height,
                RadiusStart = source.Radius, RadiusEnd = source.Radius, Length = source.Height, RayCount = source.RayCount, TiltWeight = source.TiltWeight,
                TiltPointX = source.TiltPointX, TiltPointY = source.TiltPointY, TiltPointZ = source.TiltPointZ,
                BaseOrientationX = source.BaseOrientationX, BaseOrientationY = source.BaseOrientationY, BaseOrientationZ = source.BaseOrientationZ, BaseOrientationW = source.BaseOrientationW,
                Segments = source.Segments,
            }) }));
        legacySources.AddRange(sceneState.ProjectedLightSources.Select(source => new CollisionSourceLibraryItemViewModel { TransferredSource = MapProjectedLightSource(source) }));
        foreach (var source in legacySources) service.AddToLibrary(source);
        if (sceneState.AssignedSource is not null)
        {
            var assigned = MapCollisionSource(sceneState.AssignedSource);
            service.AddToLibrary(assigned);
            scene.AssignSource(assigned);
        }
        else if (legacySources.FirstOrDefault() is { } legacyAssigned)
        {
            scene.AssignSource(legacyAssigned);
        }

        foreach (var hole in sceneState.HolePoints)
        {
            scene.HolePoints.Add(hole);
        }
        foreach (var point in sceneState.NaturalPoints ?? new List<Point3>())
            scene.NaturalPoints.Add(point);

        foreach (var corner in sceneState.MeasuredCornerPoints ?? new List<Point3>())
        {
            scene.MeasuredCornerPoints.Add(corner);
        }

        scene.ProjectionState.SelectedMethodId = sceneState.Projection.SelectedMethodId;
        scene.ProjectionState.SelectedResultKey = sceneState.Projection.SelectedResultKey;
        foreach (var result in sceneState.Projection.Results)
        {
            scene.ProjectionState.SavedResults.Add(new NamedProjectionResultState
            {
                Key = result.Key,
                DisplayName = result.Name,
                Result = new ProjectionComputationResult
                {
                    MethodId = result.MethodId,
                    PointSourceOrigin = result.PointSourceOrigin ?? (string.Equals(result.MethodId, ProjectionMethodIds.PointSource, StringComparison.OrdinalIgnoreCase) ? result.SourceFrame.Origin : null),
                    SourceFrame = new PointSourceFrameState
                    {
                        Origin = result.SourceFrame.Origin,
                        AxisX = result.SourceFrame.AxisX,
                        AxisY = result.SourceFrame.AxisY,
                        AxisZ = result.SourceFrame.AxisZ,
                    },
                    Rays = result.Rays.Select(MapProjectionRay).ToList(),
                    AxisymmetricSource = MapAxisymmetricProjectionState(result),
                },
            });
        }

        return scene;
    }

    private static CylindricalLightSourceItemViewModel MapGeneratedLightSource(AxisymmetricLightSourceState source)
    {
        var restored = new CylindricalLightSourceItemViewModel
        {
            Name = source.Name,
            SourceKind = source.SourceKind,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            RotationX = source.RotationX,
            RotationY = source.RotationY,
            RotationZ = source.RotationZ,
            Radius = source.Radius,
            Height = source.Height,
            RadiusStart = source.RadiusStart,
            RadiusEnd = source.RadiusEnd,
            Length = source.Length,
            ArcRadius = source.ArcRadius,
            OgiveCurvatureDirection = source.OgiveCurvatureDirection,
            RayCount = source.RayCount,
            TiltWeight = source.TiltWeight,
            TiltPointX = source.TiltPointX,
            TiltPointY = source.TiltPointY,
            TiltPointZ = source.TiltPointZ,
            BaseOrientation = BaseOrientationPersistence.FromComponents(
                source.BaseOrientationX,
                source.BaseOrientationY,
                source.BaseOrientationZ,
                source.BaseOrientationW),
        };

        foreach (var segment in source.Segments)
        {
            restored.HybridSegments.Add(new HybridSourceSegmentItemViewModel
            {
                SegmentKind = segment.SegmentKind,
                Length = segment.Length,
                RadiusStart = segment.RadiusStart,
                RadiusEnd = segment.RadiusEnd,
                ArcRadius = segment.ArcRadius ?? 20f,
                OgiveCurvatureDirection = segment.OgiveCurvatureDirection,
            });
        }

        return restored;
    }

    private static ProjectedLightSourceItemViewModel MapProjectedLightSource(ProjectedLightSourceState state)
    {
        var restored = new ProjectedLightSourceItemViewModel
        {
            Name = state.Name,
            SourceFrame = new PointSourceFrameState
            {
                Origin = state.SourceFrame.Origin,
                AxisX = state.SourceFrame.AxisX,
                AxisY = state.SourceFrame.AxisY,
                AxisZ = state.SourceFrame.AxisZ,
            },
            ProfileDefinition = state.ProfileDefinition,
            BaseOrientation = BaseOrientationPersistence.FromComponents(state.BaseOrientationX, state.BaseOrientationY, state.BaseOrientationZ, state.BaseOrientationW),
            OriginKind = Enum.TryParse<ProjectedLightSourceOriginKind>(state.OriginKind, ignoreCase: true, out var originKind)
                ? originKind
                : ProjectedLightSourceOriginKind.ProjectionResult,
        };

        foreach (var ray in state.Rays)
        {
            restored.Rays.Add(MapProjectionRay(ray));
        }
        foreach (var ray in state.ExactRays)
        {
            restored.ExactRays.Add(new DomainRay3D(new Vector3(ray.OriginX, ray.OriginY, ray.OriginZ), new Vector3(ray.DirectionX, ray.DirectionY, ray.DirectionZ)));
        }

        // Part 1 persisted completed physical rays in the projection metadata collection.
        // Normalize those files in memory so the next save uses the canonical exact-ray form.
        if (restored.OriginKind == ProjectedLightSourceOriginKind.CompletedProjectionResult && restored.ExactRays.Count == 0 && restored.Rays.Count > 0)
        {
            foreach (var ray in restored.Rays) restored.ExactRays.Add(ray.Ray);
            restored.Rays.Clear();
        }

        return restored;
    }

    private static AxisymmetricProjectionState? MapAxisymmetricProjectionState(ProjectionResultStateDto result)
    {
        var axisymmetric = result.AxisymmetricSource;
        var cylindrical = (AxisymmetricProjectionStateDto?)null;
        if (axisymmetric is null && cylindrical is null)
        {
            return null;
        }

        if (axisymmetric is not null)
        {
            return new AxisymmetricProjectionState
            {
                SourceFrame = new PointSourceFrameState
                {
                    Origin = axisymmetric.SourceFrame.Origin,
                    AxisX = axisymmetric.SourceFrame.AxisX,
                    AxisY = axisymmetric.SourceFrame.AxisY,
                    AxisZ = axisymmetric.SourceFrame.AxisZ,
                },
                ProfileDefinition = axisymmetric.ProfileDefinition,
                Radius = axisymmetric.ProfileDefinition.Radius,
                Length = axisymmetric.ProfileDefinition.Length,
                LocalTiltPoint = axisymmetric.LocalTiltPoint,
                EstimatedTiltWeight = axisymmetric.EstimatedTiltWeight,
                Diagnostics = axisymmetric.Diagnostics is null ? null : new SelfCalibratingAxisymmetricProjectionDiagnostics
                {
                    RegularityWeight = axisymmetric.Diagnostics.RegularityWeight,
                    CandidateScores = axisymmetric.Diagnostics.CandidateScores.Select(candidate => new SelfCalibratingAxisymmetricCandidateDiagnostics(
                        candidate.Lambda,
                        candidate.MeanFitError,
                        candidate.RegularityError,
                        candidate.Score)).ToList(),
                },
                LeastSquaresDiagnostics = axisymmetric.LeastSquaresDiagnostics is null ? null : new LeastSquaresAxisymmetricAlignmentDiagnostics
                {
                    InitialLambda = axisymmetric.LeastSquaresDiagnostics.InitialLambda,
                    RefinedLambda = axisymmetric.LeastSquaresDiagnostics.RefinedLambda,
                    InitialMeanAlignmentError = axisymmetric.LeastSquaresDiagnostics.InitialMeanAlignmentError,
                    FinalMeanAlignmentError = axisymmetric.LeastSquaresDiagnostics.FinalMeanAlignmentError,
                    FinalRmsAlignmentError = axisymmetric.LeastSquaresDiagnostics.FinalRmsAlignmentError,
                    FinalMeanAngularErrorDegrees = axisymmetric.LeastSquaresDiagnostics.FinalMeanAngularErrorDegrees,
                    FinalMaxAngularErrorDegrees = axisymmetric.LeastSquaresDiagnostics.FinalMaxAngularErrorDegrees,
                    MaxAngularErrorHoleIndex = axisymmetric.LeastSquaresDiagnostics.MaxAngularErrorHoleIndex,
                    Iterations = axisymmetric.LeastSquaresDiagnostics.Iterations,
                    Converged = axisymmetric.LeastSquaresDiagnostics.Converged,
                    UsesRegularization = axisymmetric.LeastSquaresDiagnostics.UsesRegularization,
                    IterationHistory = axisymmetric.LeastSquaresDiagnostics.IterationHistory.Select(iteration =>
                        new LeastSquaresAxisymmetricAlignmentIterationDiagnostics(
                            iteration.Iteration,
                            iteration.ObjectiveError,
                            iteration.Lambda,
                            iteration.MeanAlignmentError,
                            iteration.RmsAlignmentError,
                            iteration.MeanAngularErrorDegrees,
                            iteration.MaxAngularErrorDegrees,
                            iteration.Improved)).ToList(),
                },
                Points = axisymmetric.Points.Select(point => new AxisymmetricProjectionPoint(
                    point.HolePoint,
                    point.SourceSurfacePoint,
                    point.RayDirection,
                    point.RayOrigin)
                {
                    ModeledRayDirection = point.ModeledRayDirection,
                    LocalU = point.LocalU,
                    LocalTheta = point.LocalTheta,
                    UnwrappedU = point.UnwrappedU,
                    UnwrappedV = point.UnwrappedV,
                    FitError = point.FitError,
                    AlignmentError = point.AlignmentError,
                    AngularErrorDegrees = point.AngularErrorDegrees,
                }).ToList(),
            };
        }

        throw new InvalidOperationException("Legacy cylindrical projection payload is no longer supported.");
    }
}
