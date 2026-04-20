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
        foreach (var sceneState in state.Scenes)
        {
            sceneCollectionService.AddScene(MapScene(sceneState), selectScene: false);
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
        };

        _jsonService.SaveProject(filePath, state);
    }

    public void LoadCollisionTab(string filePath, SceneCollectionService sceneCollectionService)
    {
        var state = _jsonService.LoadProject(filePath);
        sceneCollectionService.Scenes.Clear();
        foreach (var sceneState in state.Scenes)
        {
            sceneCollectionService.AddScene(MapScene(sceneState), selectScene: false);
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
            sceneCollectionService.AddScene(MapScene(sceneState), selectScene: false);
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
            }).ToList(),
            ManualRays = scene.Rays.Select(ray => new RayState
            {
                OriginX = ray.OriginX,
                OriginY = ray.OriginY,
                OriginZ = ray.OriginZ,
                DirectionX = ray.DirectionX,
                DirectionY = ray.DirectionY,
                DirectionZ = ray.DirectionZ,
            }).ToList(),
            CylindricalLightSources = scene.LightSources.Select(source => new CylindricalLightSourceState
            {
                Name = source.Name,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                PositionZ = source.PositionZ,
                RotationX = source.RotationX,
                RotationY = source.RotationY,
                RotationZ = source.RotationZ,
                Radius = source.Radius,
                Height = source.Height,
                RayCount = source.RayCount,
            }).ToList(),
            HolePoints = scene.HolePoints.ToList(),
            Projection = new SceneProjectionStateDto
            {
                SelectedMethodId = scene.ProjectionState.SelectedMethodId,
                SelectedResultKey = scene.ProjectionState.SelectedResultKey,
                Results = scene.ProjectionState.SavedResults.Select(MapProjectionResult).ToList(),
            },
        };
    }

    private static ProjectionResultStateDto MapProjectionResult(NamedProjectionResultState namedResult)
    {
        return new ProjectionResultStateDto
        {
            Key = namedResult.Key,
            Name = namedResult.DisplayName,
            MethodId = namedResult.Result.MethodId,
            PointSourceOrigin = namedResult.Result.PointSourceOrigin,
            SourceFrame = new PointSourceFrameStateDto
            {
                Origin = namedResult.Result.SourceFrame.Origin,
                AxisX = namedResult.Result.SourceFrame.AxisX,
                AxisY = namedResult.Result.SourceFrame.AxisY,
                AxisZ = namedResult.Result.SourceFrame.AxisZ,
            },
            Rays = namedResult.Result.Rays.Select(ray => new ProjectionRayStateDto
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
            }).ToList(),
        };
    }

    private static CollisionSceneViewModel MapScene(SceneState sceneState)
    {
        var scene = new CollisionSceneViewModel(sceneState.Name);

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
                BaseOrientation = Quaternion.Identity,
            });
        }

        foreach (var ray in sceneState.ManualRays)
        {
            scene.Rays.Add(new RayItemViewModel
            {
                OriginX = ray.OriginX,
                OriginY = ray.OriginY,
                OriginZ = ray.OriginZ,
                DirectionX = ray.DirectionX,
                DirectionY = ray.DirectionY,
                DirectionZ = ray.DirectionZ,
            });
        }

        foreach (var source in sceneState.CylindricalLightSources)
        {
            scene.LightSources.Add(new CylindricalLightSourceItemViewModel
            {
                Name = source.Name,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                PositionZ = source.PositionZ,
                RotationX = source.RotationX,
                RotationY = source.RotationY,
                RotationZ = source.RotationZ,
                Radius = source.Radius,
                Height = source.Height,
                RayCount = source.RayCount,
                BaseOrientation = Quaternion.Identity,
            });
        }

        foreach (var hole in sceneState.HolePoints)
        {
            scene.HolePoints.Add(hole);
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
                    PointSourceOrigin = result.PointSourceOrigin ?? result.SourceFrame.Origin,
                    SourceFrame = new PointSourceFrameState
                    {
                        Origin = result.SourceFrame.Origin,
                        AxisX = result.SourceFrame.AxisX,
                        AxisY = result.SourceFrame.AxisY,
                        AxisZ = result.SourceFrame.AxisZ,
                    },
                    Rays = result.Rays.Select(ray => new ProjectionRay(
                        new DomainRay3D(
                            new Vector3(ray.Ray.OriginX, ray.Ray.OriginY, ray.Ray.OriginZ),
                            new Vector3(ray.Ray.DirectionX, ray.Ray.DirectionY, ray.Ray.DirectionZ)),
                        ray.TargetHolePoint)).ToList(),
                },
            });
        }

        return scene;
    }
}
