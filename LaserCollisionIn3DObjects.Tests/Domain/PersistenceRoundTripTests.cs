using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Persistence;
using LaserCollisionIn3DObjects.Domain.Projection;
using System.Numerics;
using System.Text.Json;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public class PersistenceRoundTripTests
{
    [Fact]
    public void SceneMeasuredCornerPoints_RoundTrip_AndMissingFieldDefaultsEmpty()
    {
        var service = new JsonStateFileService();
        var filePath = Path.GetTempFileName();
        try
        {
            var expected = new Point3(1.25, -2.5, 3.75);
            var state = new ProjectState { Scenes = { new SceneState { Name = "measured", MeasuredCornerPoints = { expected } } } };
            service.SaveProject(filePath, state);
            Assert.Equal(expected, Assert.Single(service.LoadProject(filePath).Scenes[0].MeasuredCornerPoints));

            File.WriteAllText(filePath, "{\"schemaVersion\":1,\"scenes\":[{\"name\":\"old\"}]}");
            Assert.Empty(service.LoadProject(filePath).Scenes[0].MeasuredCornerPoints);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

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
                        NaturalPoints = [new Point3(4, 5, 6)],
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
                    IncludeNaturalPoints = false,
                    SelectedSceneName = "Scene A",
                    SelectedMethodId = "point-source",
                    TiltPointX = 12.5,
                    TiltPointY = -6.25,
                    TiltPointZ = 3.75,
                    HybridTiltPointX = 12.5f,
                    HybridTiltPointY = -6.25f,
                    HybridTiltPointZ = 3.75f,
                },
            };

            service.SaveProject(filePath, state);
            var roundTrip = service.LoadProject(filePath);

            Assert.Equal(1, roundTrip.SchemaVersion);
            Assert.Single(roundTrip.Scenes);
            Assert.Equal("Scene A", roundTrip.Scenes[0].Name);
            Assert.Equal(new Point3(4, 5, 6), Assert.Single(roundTrip.Scenes[0].NaturalPoints));
            Assert.False(roundTrip.ProjectionWorkspace.IncludeNaturalPoints);
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
            Assert.Equal(12.5, roundTrip.ProjectionWorkspace.TiltPointX, 6);
            Assert.Equal(-6.25, roundTrip.ProjectionWorkspace.TiltPointY, 6);
            Assert.Equal(3.75, roundTrip.ProjectionWorkspace.TiltPointZ, 6);
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
    public void AxisymmetricSource_BaseOrientation_RoundTrip_PreservesQuaternionAndFinalOrientation()
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
    public void AxisymmetricSource_MissingTiltPointFields_DefaultsToOrigin()
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


    [Fact]
    public void ProjectionState_CylindricalResult_RoundTrips()
    {
        var state = new ProjectState
        {
            Scenes =
            [
                new SceneState
                {
                    Name = "Projection Scene",
                    Projection = new SceneProjectionStateDto
                    {
                        SelectedMethodId = ProjectionMethodIds.AxisymmetricSource,
                        Results =
                        [
                            new ProjectionResultStateDto
                            {
                                Key = "proj-1",
                                Name = "Cyl",
                                MethodId = ProjectionMethodIds.AxisymmetricSource,
                                SourceFrame = new PointSourceFrameStateDto
                                {
                                    Origin = new Point3(1, 2, 3),
                                    AxisX = new Vector3D(1, 0, 0),
                                    AxisY = new Vector3D(0, 1, 0),
                                    AxisZ = new Vector3D(0, 0, 1),
                                },
                                AxisymmetricSource = new AxisymmetricProjectionStateDto
                                {
                                    SourceFrame = new PointSourceFrameStateDto
                                    {
                                        Origin = new Point3(1, 2, 3),
                                        AxisX = new Vector3D(1, 0, 0),
                                        AxisY = new Vector3D(0, 1, 0),
                                        AxisZ = new Vector3D(0, 0, 1),
                                    },
                                    ProfileDefinition = new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 4, Length = 12 },
                                    Points =
                                    [
                                        new AxisymmetricProjectionPointStateDto
                                        {
                                            HolePoint = new Point3(4, 0, 0),
                                            SourceSurfacePoint = new Point3(0, 4, 0),
                                            RayOrigin = new Point3(0, 4, 0),
                                            RayDirection = new Vector3D(1, 0, 0),
                                        },
                                    ],
                                },
                            },
                        ],
                    },
                },
            ],
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<ProjectState>(json)!;
        var result = restored.Scenes[0].Projection.Results[0];

        Assert.NotNull(result.AxisymmetricSource);
        Assert.Equal(4d, result.AxisymmetricSource!.ProfileDefinition.Radius, 6);
        Assert.Equal(12d, result.AxisymmetricSource.ProfileDefinition.Length, 6);
        Assert.Single(result.AxisymmetricSource.Points);
        Assert.Equal(new Point3(0, 4, 0), result.AxisymmetricSource.Points[0].SourceSurfacePoint);
    }

    [Fact]
    public void SceneState_ProjectionOnlyFlag_RoundTrips()
    {
        var state = new ProjectState
        {
            Scenes =
            [
                new SceneState
                {
                    Name = "Imported Holes",
                    IsProjectionOnly = true,
                    HolePoints = [new Point3(1, 2, 3)],
                },
            ],
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<ProjectState>(json)!;

        Assert.Single(restored.Scenes);
        Assert.True(restored.Scenes[0].IsProjectionOnly);
}

    [Fact]
    public void ProjectState_RoundTrip_PreservesSelfCalibratingCylindricalMetadata()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-self-cal-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                Scenes =
                [
                    new SceneState
                    {
                        Name = "Scene SC",
                        Projection = new SceneProjectionStateDto
                        {
                            SelectedMethodId = ProjectionMethodIds.SelfCalibratingAxisymmetricSource,
                            Results =
                            [
                                new ProjectionResultStateDto
                                {
                                    Key = "k",
                                    Name = "r",
                                    MethodId = ProjectionMethodIds.SelfCalibratingAxisymmetricSource,
                                    SourceFrame = new PointSourceFrameStateDto
                                    {
                                        Origin = new Point3(0, 0, 0),
                                        AxisX = new Vector3D(1, 0, 0),
                                        AxisY = new Vector3D(0, 1, 0),
                                        AxisZ = new Vector3D(0, 0, 1),
                                    },
                                    AxisymmetricSource = new AxisymmetricProjectionStateDto
                                    {
                                        SourceFrame = new PointSourceFrameStateDto
                                        {
                                            Origin = new Point3(0, 0, 0),
                                            AxisX = new Vector3D(1, 0, 0),
                                            AxisY = new Vector3D(0, 1, 0),
                                            AxisZ = new Vector3D(0, 0, 1),
                                        },
                                        ProfileDefinition = new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1.5f, Length = 6f },
                                        LocalTiltPoint = new Point3(1, 2, 3),
                                        EstimatedTiltWeight = 0.42,
                                        Diagnostics = new SelfCalibratingAxisymmetricProjectionDiagnosticsDto
                                        {
                                            RegularityWeight = 0.1,
                                            CandidateScores =
                                            [
                                                new SelfCalibratingAxisymmetricCandidateDiagnosticsDto
                                                {
                                                    Lambda = 0.42,
                                                    MeanFitError = 0.01,
                                                    RegularityError = 0.02,
                                                    Score = 0.012,
                                                },
                                            ],
                                        },
                                        Points =
                                        [
                                            new AxisymmetricProjectionPointStateDto
                                            {
                                                HolePoint = new Point3(9, 9, 9),
                                                SourceSurfacePoint = new Point3(1, 1, 1),
                                                RayOrigin = new Point3(1, 1, 1),
                                                RayDirection = new Vector3D(1, 0, 0),
                                                ModeledRayDirection = new Vector3D(0, 1, 0),
                                                LocalU = 2,
                                                LocalTheta = 0.5,
                                                UnwrappedU = 2,
                                                UnwrappedV = 0.75,
                                                FitError = 0.03,
                                            },
                                        ],
                                    },
                                },
                            ],
                        },
                    },
                ],
            };

            service.SaveProject(filePath, state);
            var roundTrip = service.LoadProject(filePath);

            var result = roundTrip.Scenes[0].Projection.Results[0].AxisymmetricSource!;
            Assert.Equal(0.42, result.EstimatedTiltWeight ?? 0d, 6);
            Assert.Equal(new Point3(1, 2, 3), result.LocalTiltPoint);
            Assert.NotNull(result.Diagnostics);
            Assert.Single(result.Diagnostics!.CandidateScores);
            Assert.Equal(0.03, result.Points[0].FitError ?? 0d, 6);
            Assert.Equal(new Vector3D(0, 1, 0), result.Points[0].ModeledRayDirection);
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
    public void ProjectState_RoundTrip_PreservesLeastSquaresCylindricalAlignmentMetadata()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-ls-align-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                Scenes =
                [
                    new SceneState
                    {
                        Name = "Scene LS",
                        Projection = new SceneProjectionStateDto
                        {
                            SelectedMethodId = ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource,
                            Results =
                            [
                                new ProjectionResultStateDto
                                {
                                    Key = "k-ls",
                                    Name = "r-ls",
                                    MethodId = ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource,
                                    SourceFrame = new PointSourceFrameStateDto
                                    {
                                        Origin = new Point3(0, 0, 0),
                                        AxisX = new Vector3D(1, 0, 0),
                                        AxisY = new Vector3D(0, 1, 0),
                                        AxisZ = new Vector3D(0, 0, 1),
                                    },
                                    AxisymmetricSource = new AxisymmetricProjectionStateDto
                                    {
                                        SourceFrame = new PointSourceFrameStateDto
                                        {
                                            Origin = new Point3(0, 0, 0),
                                            AxisX = new Vector3D(1, 0, 0),
                                            AxisY = new Vector3D(0, 1, 0),
                                            AxisZ = new Vector3D(0, 0, 1),
                                        },
                                        ProfileDefinition = new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1.5f, Length = 6f },
                                        LocalTiltPoint = new Point3(1, 2, 3),
                                        EstimatedTiltWeight = 0.31,
                                        LeastSquaresDiagnostics = new LeastSquaresAxisymmetricAlignmentDiagnosticsDto
                                        {
                                            InitialLambda = 0.34,
                                            RefinedLambda = 0.31,
                                            InitialMeanAlignmentError = 0.04,
                                            FinalMeanAlignmentError = 0.01,
                                            FinalRmsAlignmentError = 0.015,
                                            FinalMeanAngularErrorDegrees = 1.1,
                                            FinalMaxAngularErrorDegrees = 3.3,
                                            MaxAngularErrorHoleIndex = 2,
                                            Iterations = 7,
                                            Converged = true,
                                            UsesRegularization = false,
                                            IterationHistory =
                                            [
                                                new AxisymmetricLeastSquaresIterationDiagnosticsDto
                                                {
                                                    Iteration = 1,
                                                    ObjectiveError = 0.005,
                                                    Lambda = 0.32,
                                                    MeanAlignmentError = 0.02,
                                                    RmsAlignmentError = 0.025,
                                                    MeanAngularErrorDegrees = 1.8,
                                                    MaxAngularErrorDegrees = 2.9,
                                                    Improved = true,
                                                },
                                            ],
                                        },
                                        Points =
                                        [
                                            new AxisymmetricProjectionPointStateDto
                                            {
                                                HolePoint = new Point3(9, 9, 9),
                                                SourceSurfacePoint = new Point3(1, 1, 1),
                                                RayOrigin = new Point3(1, 1, 1),
                                                RayDirection = new Vector3D(1, 0, 0),
                                                ModeledRayDirection = new Vector3D(0, 1, 0),
                                                LocalU = 2,
                                                LocalTheta = 0.5,
                                                UnwrappedU = 2,
                                                UnwrappedV = 0.75,
                                                AlignmentError = 0.03,
                                                AngularErrorDegrees = 1.2,
                                                FitError = 0.03,
                                            },
                                        ],
                                    },
                                },
                            ],
                        },
                    },
                ],
            };

            service.SaveProject(filePath, state);
            var roundTrip = service.LoadProject(filePath);

            var result = roundTrip.Scenes[0].Projection.Results[0].AxisymmetricSource!;
            Assert.Equal(0.31, result.EstimatedTiltWeight ?? 0d, 6);
            Assert.NotNull(result.LeastSquaresDiagnostics);
            Assert.Equal(0.34, result.LeastSquaresDiagnostics!.InitialLambda, 6);
            Assert.Equal(0.31, result.LeastSquaresDiagnostics.RefinedLambda, 6);
            Assert.False(result.LeastSquaresDiagnostics.UsesRegularization);
            Assert.Single(result.LeastSquaresDiagnostics.IterationHistory);
            Assert.Equal(0.005, result.LeastSquaresDiagnostics.IterationHistory[0].ObjectiveError, 6);
            Assert.Equal(0.025, result.LeastSquaresDiagnostics.IterationHistory[0].RmsAlignmentError, 6);
            Assert.True(result.LeastSquaresDiagnostics.IterationHistory[0].Improved);
            Assert.Equal(0.03, result.Points[0].AlignmentError ?? 0d, 6);
            Assert.Equal(1.2, result.Points[0].AngularErrorDegrees ?? 0d, 6);
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
    public void AxisymmetricLightSources_RoundTrip_PreservesFrustumAndOgiveParameters()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-axis-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                Scenes =
                [
                    new SceneState
                    {
                        Name = "Scene A",
                        LightSources =
                        [
                            new AxisymmetricLightSourceState
                            {
                                Name = "Frustum",
                                SourceKind = AxisymmetricSourceKind.ConicalFrustum,
                                RadiusStart = 2f,
                                RadiusEnd = 3f,
                                Length = 4f,
                                RayCount = 25,
                            },
                            new AxisymmetricLightSourceState
                            {
                                Name = "Ogive",
                                SourceKind = AxisymmetricSourceKind.CircularOgive,
                                RadiusStart = 2f,
                                RadiusEnd = 4f,
                                Length = 5f,
                                ArcRadius = 10f,
                                OgiveCurvatureDirection = OgiveCurvatureDirection.Inward,
                                RayCount = 36,
                            },
                        ],
                    },
                ],
            };

            service.SaveProject(filePath, state);
            var restored = service.LoadProject(filePath);

            Assert.Equal(2, restored.Scenes[0].LightSources.Count);
            Assert.Equal(AxisymmetricSourceKind.ConicalFrustum, restored.Scenes[0].LightSources[0].SourceKind);
            Assert.Equal(3f, restored.Scenes[0].LightSources[0].RadiusEnd, 3);
            Assert.Equal(AxisymmetricSourceKind.CircularOgive, restored.Scenes[0].LightSources[1].SourceKind);
            Assert.Equal(OgiveCurvatureDirection.Inward, restored.Scenes[0].LightSources[1].OgiveCurvatureDirection);
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
    public void AxisymmetricLightSources_RoundTrip_PreservesHybridSegments()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-hybrid-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                Scenes =
                [
                    new SceneState
                    {
                        Name = "Scene Hybrid",
                        LightSources =
                        [
                            new AxisymmetricLightSourceState
                            {
                                Name = "Hybrid",
                                SourceKind = AxisymmetricSourceKind.Hybrid,
                                RayCount = 12,
                                Segments =
                                [
                                    new AxisymmetricSourceSegmentStateDto { SegmentKind = HybridAxisymmetricSourceSegmentKind.Cylinder, Length = 2f, RadiusStart = 3f, RadiusEnd = 3f },
                                    new AxisymmetricSourceSegmentStateDto { SegmentKind = HybridAxisymmetricSourceSegmentKind.ConicalFrustum, Length = 2f, RadiusStart = 3f, RadiusEnd = 4f },
                                ],
                            },
                        ],
                    },
                ],
                ProjectionWorkspace = new ProjectionWorkspaceStateDto
                {
                    HybridSegmentCount = 2,
                    HybridSegments =
                    [
                        new AxisymmetricSourceSegmentStateDto { SegmentKind = HybridAxisymmetricSourceSegmentKind.Cylinder, Length = 1f, RadiusStart = 2f, RadiusEnd = 2f },
                        new AxisymmetricSourceSegmentStateDto { SegmentKind = HybridAxisymmetricSourceSegmentKind.ConicalFrustum, Length = 1f, RadiusStart = 2f, RadiusEnd = 3f },
                    ],
                },
            };

            service.SaveProject(filePath, state);
            var restored = service.LoadProject(filePath);

            Assert.Equal(AxisymmetricSourceKind.Hybrid, restored.Scenes[0].LightSources[0].SourceKind);
            Assert.Equal(2, restored.Scenes[0].LightSources[0].Segments.Count);
            Assert.Equal(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, restored.Scenes[0].LightSources[0].Segments[1].SegmentKind);
            Assert.Equal(2, restored.ProjectionWorkspace.HybridSegments.Count);
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
    public void ProjectionWorkspaceState_RoundTrip_PreservesGeometryKind()
    {
        var service = new JsonStateFileService();
        var filePath = Path.Combine(Path.GetTempPath(), $"lc3d-geom-{Guid.NewGuid():N}.json");

        try
        {
            var state = new ProjectState
            {
                ProjectionWorkspace = new ProjectionWorkspaceStateDto
                {
                    ProjectionGeometryKind = AxisymmetricSourceKind.CircularOgive,
                    GeometryRadiusStart = 2.5,
                    GeometryRadiusEnd = 1.25,
                    GeometryLength = 11,
                    GeometryArcRadius = 24,
                },
            };

            service.SaveProject(filePath, state);
            var restored = service.LoadProject(filePath);

            Assert.Equal(AxisymmetricSourceKind.CircularOgive, restored.ProjectionWorkspace.ProjectionGeometryKind);
            Assert.Equal(2.5, restored.ProjectionWorkspace.GeometryRadiusStart, 6);
            Assert.Equal(1.25, restored.ProjectionWorkspace.GeometryRadiusEnd, 6);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

}
