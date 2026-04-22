using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Persistence;
using System.Numerics;
using System.Text.Json;

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
                                TiltPointX = 4f,
                                TiltPointY = -1.5f,
                                TiltPointZ = 2f,
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
            Assert.Equal(4f, roundTrip.Scenes[0].CylindricalLightSources[0].TiltPointX, 3);
            Assert.Equal(-1.5f, roundTrip.Scenes[0].CylindricalLightSources[0].TiltPointY, 3);
            Assert.Equal(2f, roundTrip.Scenes[0].CylindricalLightSources[0].TiltPointZ, 3);
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

    [Fact]
    public void ProjectState_DoesNotContainGraphicMasterStoredCharts()
    {
        var state = new ProjectState();
        var json = JsonSerializer.Serialize(state);

        Assert.DoesNotContain("storedCharts", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("graphicMaster", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrismState_BaseOrientation_RoundTrip_PreservesQuaternion()
    {
        var orientation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.3f, 0.5f, -0.2f));
        var state = new ProjectState
        {
            Scenes =
            [
                new SceneState
                {
                    Name = "Scene",
                    Prisms =
                    [
                        new PrismState
                        {
                            Name = "P1",
                            BaseOrientationX = orientation.X,
                            BaseOrientationY = orientation.Y,
                            BaseOrientationZ = orientation.Z,
                            BaseOrientationW = orientation.W,
                        },
                    ],
                },
            ],
        };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<ProjectState>(json)!;
        var prism = loaded.Scenes[0].Prisms[0];
        var roundTrip = BaseOrientationPersistence.FromComponents(prism.BaseOrientationX, prism.BaseOrientationY, prism.BaseOrientationZ, prism.BaseOrientationW);
        Assert.True(Quaternion.Dot(orientation, roundTrip) > 0.9999f);
    }

    [Fact]
    public void CylindricalSource_BaseOrientation_RoundTrip_PreservesQuaternionAndFinalOrientation()
    {
        var baseOrientation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f));
        const float rx = 10f;
        const float ry = 20f;
        const float rz = -5f;

        var sourceState = new CylindricalLightSourceState
        {
            Name = "S1",
            RotationX = rx,
            RotationY = ry,
            RotationZ = rz,
            BaseOrientationX = baseOrientation.X,
            BaseOrientationY = baseOrientation.Y,
            BaseOrientationZ = baseOrientation.Z,
            BaseOrientationW = baseOrientation.W,
        };

        var restoredBase = BaseOrientationPersistence.FromComponents(
            sourceState.BaseOrientationX,
            sourceState.BaseOrientationY,
            sourceState.BaseOrientationZ,
            sourceState.BaseOrientationW);

        var before = FrameOrientationBuilder.ApplyLocalEulerDegrees(baseOrientation, rx, ry, rz);
        var after = FrameOrientationBuilder.ApplyLocalEulerDegrees(restoredBase, rx, ry, rz);
        Assert.True(Quaternion.Dot(before, after) > 0.9999f);
    }

    [Fact]
    public void MissingBaseOrientationFields_FallbacksToIdentity()
    {
        var prism = new PrismState { Name = "Legacy Prism" };
        var source = new CylindricalLightSourceState { Name = "Legacy Source" };
        Assert.Equal(Quaternion.Identity, BaseOrientationPersistence.FromComponents(prism.BaseOrientationX, prism.BaseOrientationY, prism.BaseOrientationZ, prism.BaseOrientationW));
        Assert.Equal(Quaternion.Identity, BaseOrientationPersistence.FromComponents(source.BaseOrientationX, source.BaseOrientationY, source.BaseOrientationZ, source.BaseOrientationW));
        Assert.Equal(0f, source.TiltPointX);
        Assert.Equal(0f, source.TiltPointY);
        Assert.Equal(0f, source.TiltPointZ);
    }

    [Fact]
    public void ArrayPlacementOrientations_RoundTrip_StayFacingOrigin()
    {
        var placements = PrismPlacementGenerator.CreateCylindricalPlacements(radius: 10f, count: 6);

        var states = placements.Select((placement, i) => new PrismState
        {
            Name = $"P{i}",
            BaseOrientationX = placement.Orientation.X,
            BaseOrientationY = placement.Orientation.Y,
            BaseOrientationZ = placement.Orientation.Z,
            BaseOrientationW = placement.Orientation.W,
        }).ToList();

        var json = JsonSerializer.Serialize(states);
        var restored = JsonSerializer.Deserialize<List<PrismState>>(json)!;

        for (var i = 0; i < placements.Count; i++)
        {
            var expected = placements[i].Orientation;
            var actual = BaseOrientationPersistence.FromComponents(
                restored[i].BaseOrientationX,
                restored[i].BaseOrientationY,
                restored[i].BaseOrientationZ,
                restored[i].BaseOrientationW);
            Assert.True(Quaternion.Dot(expected, actual) > 0.9999f);
        }
    }

    [Fact]
    public void CylindricalSource_MissingTiltPointFields_DefaultsToOrigin()
    {
        const string legacyJson = """
                                  {
                                    "schemaVersion": 1,
                                    "scenes": [
                                      {
                                        "Name": "Legacy",
                                        "CylindricalLightSources": [
                                          {
                                            "Name": "L1",
                                            "Radius": 2,
                                            "Height": 4,
                                            "RayCount": 12,
                                            "TiltWeight": 0.1
                                          }
                                        ]
                                      }
                                    ]
                                  }
                                  """;

        var restored = JsonSerializer.Deserialize<ProjectState>(legacyJson)!;
        var source = restored.Scenes[0].CylindricalLightSources[0];

        Assert.Equal(0f, source.TiltPointX);
        Assert.Equal(0f, source.TiltPointY);
        Assert.Equal(0f, source.TiltPointZ);
    }
}
