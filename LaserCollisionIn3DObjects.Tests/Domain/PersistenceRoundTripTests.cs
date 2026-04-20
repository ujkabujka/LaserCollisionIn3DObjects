using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Persistence;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public class PersistenceRoundTripTests
{
    [Fact]
    public void ProjectState_RoundTrip_PreservesProjectionAndAnnotationState()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                Scenes =
                [
                    new SceneState
                    {
                        Name = "Scene A",
                        CylindricalLightSources =
                        [
                            new CylindricalLightSourceState
                            {
                                Name = "Light Source 1",
                                Radius = 3,
                                Height = 5,
                                RayCount = 10,
                                TiltWeight = 0.25f,
                            },
                        ],
                        HolePoints = [new Point3(1, 2, 3)],
                        Projection = new SceneProjectionStateDto
                        {
                            SelectedMethodId = "point-source",
                            SelectedResultKey = "projection.result.1",
                            Results =
                            [
                                new ProjectionResultStateDto
                                {
                                    Key = "projection.result.1",
                                    Name = "Result 1",
                                    MethodId = "point-source",
                                    PointSourceOrigin = new Point3(1, 1, 1),
                                    SourceFrame = new PointSourceFrameStateDto
                                    {
                                        Origin = new Point3(0, 0, 0),
                                        AxisX = new Vector3D(1, 0, 0),
                                        AxisY = new Vector3D(0, 1, 0),
                                        AxisZ = new Vector3D(0, 0, 1),
                                    },
                                    Rays =
                                    [
                                        new ProjectionRayStateDto
                                        {
                                            Ray = new RayState { OriginX = 0, OriginY = 0, OriginZ = 0, DirectionX = 1, DirectionY = 0, DirectionZ = 0 },
                                            TargetHolePoint = new Point3(1, 0, 0),
                                        },
                                    ],
                                },
                            ],
                        },
                    },
                ],
                AnnotationWorkspace = new AnnotationWorkspaceState
                {
                    FolderPath = "/missing/path",
                    IsFolderResolved = false,
                    GlobalPanelWidthMm = 1000,
                    GlobalPanelHeightMm = 1000,
                    GlobalPanelThicknessMm = 10,
                },
                ProjectionWorkspace = new ProjectionWorkspaceStateDto
                {
                    SelectedSceneName = "Scene A",
                    SelectedMethodId = "point-source",
                },
            };

            service.SaveProject(filePath, state);
            var roundTrip = service.LoadProject(filePath);

            Assert.Equal(1, roundTrip.SchemaVersion);
            Assert.Single(roundTrip.Scenes);
            Assert.Equal("Scene A", roundTrip.Scenes[0].Name);
            Assert.Single(roundTrip.Scenes[0].Projection.Results);
            Assert.Equal("Result 1", roundTrip.Scenes[0].Projection.Results[0].Name);
            Assert.Equal(new Point3(1, 1, 1), roundTrip.Scenes[0].Projection.Results[0].PointSourceOrigin);
            Assert.Single(roundTrip.Scenes[0].CylindricalLightSources);
            Assert.Equal(0.25f, roundTrip.Scenes[0].CylindricalLightSources[0].TiltWeight, 3);
            Assert.False(roundTrip.AnnotationWorkspace.IsFolderResolved);
            Assert.Equal("/missing/path", roundTrip.AnnotationWorkspace.FolderPath);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void StandaloneTabFile_RoundTrip_IsValidProjectEnvelope()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-tab-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                AnnotationWorkspace = new AnnotationWorkspaceState
                {
                    FolderPath = "/tmp/folder",
                    IsFolderResolved = true,
                },
            };

            service.SaveProject(filePath, state);
            var loaded = service.LoadProject(filePath);

            Assert.Equal(1, loaded.SchemaVersion);
            Assert.NotNull(loaded.AnnotationWorkspace);
            Assert.Empty(loaded.Scenes);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void AnnotationStateResolver_MissingFolder_IsUnresolved()
    {
        var state = new AnnotationWorkspaceState
        {
            FolderPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"),
        };

        Assert.False(AnnotationStateResolver.IsFolderResolved(state));
    }
}
