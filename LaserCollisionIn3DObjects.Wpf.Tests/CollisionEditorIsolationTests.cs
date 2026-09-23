using System.IO;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using DomainRay3D = LaserCollisionIn3DObjects.Domain.Geometry.Ray3D;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class CollisionEditorIsolationTests
{
    private const string ValidRow = "2000,3000,10,10,0,0,10.198039,11.309932,0,10.630146,11.309932,16.392523,10.440307,0,16.699244";

    [Fact]
    public void EighteenMeasuredPrisms_SelectedLastPrism_CannotInterceptSourceApply()
    {
        CollisionEditorStaTest.Run(() =>
        {
            var vm = CreateViewModel();
            var scene = Assert.IsType<CollisionSceneViewModel>(vm.SelectedScene);
            Assert.True(vm.ImportMeasuredPrisms(new StringReader(string.Join('\n', Enumerable.Repeat(ValidRow, 18)))));
            var prism = Assert.IsType<PrismItemViewModel>(scene.SelectedPrism);
            var prismsBefore = scene.Prisms.Select(PrismState).ToArray();

            var fileFrame = new Frame3D(new Vector3(8, -4, 6), FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, 11, 22, -33));
            var file = new LightSourceTransferData(
                "Imported", new AxisymmetricSourceProfileDefinition(), fileFrame, 0, Vector3.Zero,
                [new LightSourceTransferRay(new Vector3(2, 1, -1), Vector3.Normalize(new Vector3(1, 2, 3)))]);
            Assert.True(vm.ImportLightSourceIntoCollision(file));
            var assigned = Assert.IsType<ProjectedLightSourceItemViewModel>(scene.AssignedSource!.TransferredSource);
            var library = Assert.Single(vm.AvailableSources).TransferredSource!;
            Assert.Equal(new Point3(0, 0, 0), assigned.SourceFrame.Origin);
            Assert.Equal(new Point3(0, 0, 0), library.SourceFrame.Origin);
            Assert.Equal(0, vm.SelectedSourceEditPositionX);
            Assert.Equal(0, vm.SelectedSourceEditRotationX, 4);
            var oldFrame = assigned.SourceFrame;
            var oldRay = assigned.ExactRays[0];
            scene.PublishCollisionResults([], []);
            vm.SelectedSourceEditPositionX = 5;
            vm.SelectedSourceEditPositionY = 2;
            vm.SelectedSourceEditPositionZ = 1;
            vm.SelectedSourceEditRotationX = 15;
            vm.SelectedSourceEditRotationY = -25;
            vm.SelectedSourceEditRotationZ = 40;

            Assert.True(vm.ApplySelectedSourceChangesCommand.CanExecute(null));
            vm.ApplySelectedSourceChangesCommand.Execute(null);

            Assert.Equal(prismsBefore, scene.Prisms.Select(PrismState));
            Assert.Equal(new Point3(5, 2, 1), assigned.SourceFrame.Origin);
            Assert.Equal(new Point3(0, 0, 0), library.SourceFrame.Origin);
            var expectedOrientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, 15, -25, 40);
            AssertOrientationEquivalent(expectedOrientation, TransferredLightSourcePoseService.GetOrientation(assigned.SourceFrame));
            AssertRayClose(TransferredLightSourcePoseService.TransformRay(oldRay, oldFrame, assigned.SourceFrame), assigned.ExactRays[0]);
            Assert.False(scene.HasValidCollisionRun);
        });
    }

    [Fact]
    public void PrismApply_ChangesOnlySelectedPrism_WhenSourceIsAssigned()
    {
        CollisionEditorStaTest.Run(() =>
        {
            var vm = CreateViewModel();
            var scene = Assert.IsType<CollisionSceneViewModel>(vm.SelectedScene);
            var prism = new PrismItemViewModel { PositionX = 1, SizeX = 2, SizeY = 3, SizeZ = 4 };
            scene.Prisms.Add(prism);
            vm.SelectedPrism = prism;
            scene.AssignSource(new CollisionSourceLibraryItemViewModel { TransferredSource = TransferredSource() });
            var source = scene.AssignedSource!.TransferredSource!;
            var frameBefore = source.SourceFrame;
            var rayBefore = source.ExactRays[0];

            vm.SelectedPrismEditPositionX = 9;
            vm.SelectedPrismEditSizeX = 8;
            vm.ApplySelectedPrismChangesCommand.Execute(null);

            Assert.Equal(9, prism.PositionX);
            Assert.Equal(8, prism.SizeX);
            Assert.Equal(frameBefore, source.SourceFrame);
            Assert.Equal(rayBefore, source.ExactRays[0]);
        });
    }

    [Fact]
    public void PrismAndSourceEditors_HaveIndependentStateAndCanExecute()
    {
        CollisionEditorStaTest.Run(() =>
        {
            var vm = CreateViewModel();
            var scene = vm.SelectedScene!;
            var prism = new PrismItemViewModel { PositionX = 12, SizeX = 2, SizeY = 2, SizeZ = 2 };
            scene.Prisms.Add(prism);
            vm.SelectedPrism = prism;
            vm.SelectedSourceEditPositionX = 77;
            vm.SelectedPrism = prism;
            Assert.Equal(77, vm.SelectedSourceEditPositionX);
            Assert.True(vm.ApplySelectedPrismChangesCommand.CanExecute(null));
            Assert.False(vm.ApplySelectedSourceChangesCommand.CanExecute(null));

            scene.AssignSource(new CollisionSourceLibraryItemViewModel { TransferredSource = TransferredSource() });
            Assert.True(vm.ApplySelectedPrismChangesCommand.CanExecute(null));
            Assert.True(vm.ApplySelectedSourceChangesCommand.CanExecute(null));
            Assert.Equal(12, vm.SelectedPrismEditPositionX);
        });
    }

    [Fact]
    public void TransferredSource_NoChangeAndRepeatedAbsoluteApply_DoNotDrift()
    {
        CollisionEditorStaTest.Run(() =>
        {
            var vm = CreateViewModel();
            var scene = vm.SelectedScene!;
            var orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, 21, -17, 33);
            var source = TransferredSource(new Vector3(4, -3, 2), orientation);
            scene.AssignSource(new CollisionSourceLibraryItemViewModel { TransferredSource = source });
            var assigned = scene.AssignedSource!.TransferredSource!;
            var frameBefore = assigned.SourceFrame;
            var rayBefore = assigned.ExactRays[0];
            vm.ApplySelectedSourceChangesCommand.Execute(null);
            AssertFrameClose(frameBefore, assigned.SourceFrame);
            AssertRayClose(rayBefore, assigned.ExactRays[0]);

            vm.SelectedSourceEditPositionX = 3; vm.SelectedSourceEditPositionY = -2; vm.SelectedSourceEditPositionZ = 7;
            vm.SelectedSourceEditRotationX = 15; vm.SelectedSourceEditRotationY = -25; vm.SelectedSourceEditRotationZ = 40;
            vm.ApplySelectedSourceChangesCommand.Execute(null);
            var onceFrame = assigned.SourceFrame; var onceRay = assigned.ExactRays[0];
            vm.ApplySelectedSourceChangesCommand.Execute(null);
            vm.ApplySelectedSourceChangesCommand.Execute(null);
            AssertFrameClose(onceFrame, assigned.SourceFrame);
            AssertRayClose(onceRay, assigned.ExactRays[0]);
        });
    }

    private static MainWindowViewModel CreateViewModel() => new(
        new SceneRenderSyncService(new HelixViewport3D()),
        new ProjectionRenderSyncService(new HelixViewport3D()));

    private static ProjectedLightSourceItemViewModel TransferredSource(Vector3? origin = null, Quaternion? orientation = null)
    {
        var frame = TransferredLightSourcePoseService.CreateFrame(origin ?? new Vector3(1, 2, 3), orientation ?? Quaternion.Identity);
        var source = new ProjectedLightSourceItemViewModel { Name = "Transferred", SourceFrame = frame, BaseOrientation = orientation ?? Quaternion.Identity };
        source.ExactRays.Add(new DomainRay3D((origin ?? new Vector3(1, 2, 3)) + new Vector3(2, 1, -1), Vector3.Normalize(new Vector3(1, 2, 3))));
        return source;
    }

    private static (float, float, float, float, float, float, float, float, float) PrismState(PrismItemViewModel p) =>
        (p.PositionX, p.PositionY, p.PositionZ, p.RotationX, p.RotationY, p.RotationZ, p.SizeX, p.SizeY, p.SizeZ);

    private static void AssertFrameClose(PointSourceFrameState expected, PointSourceFrameState actual)
    {
        AssertPointClose(expected.Origin, actual.Origin);
        AssertOrientationEquivalent(TransferredLightSourcePoseService.GetOrientation(expected), TransferredLightSourcePoseService.GetOrientation(actual));
    }

    private static void AssertPointClose(Point3 expected, Point3 actual)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, 1e-5);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, 1e-5);
        Assert.InRange(Math.Abs(expected.Z - actual.Z), 0, 1e-5);
    }

    private static void AssertRayClose(DomainRay3D expected, DomainRay3D actual)
    {
        AssertVectorClose(expected.Origin, actual.Origin);
        AssertVectorClose(expected.Direction, actual.Direction);
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual) => Assert.InRange(Vector3.Distance(expected, actual), 0, 1e-5);
    private static void AssertOrientationEquivalent(Quaternion expected, Quaternion actual) => Assert.InRange(1 - Math.Abs(Quaternion.Dot(Quaternion.Normalize(expected), Quaternion.Normalize(actual))), 0, 1e-5);
}

internal static class CollisionEditorStaTest
{
    public static void Run(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (exception is not null) ExceptionDispatchInfo.Capture(exception).Throw();
    }
}

public sealed class LightSourceTransferImportPolicyTests
{
    [Fact]
    public void CanonicalImport_UsesIdentityFrameAndSourceLocalRays_WithoutMutatingFile()
    {
        var service = new LightSourceTransferService();
        var orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, 20, -30, 45);
        var fileFrame = new Frame3D(new Vector3(5, -2, 3), orientation);
        var localRay = new LightSourceTransferRay(new Vector3(1, 2, 3), Vector3.Normalize(new Vector3(2, -1, 4)));
        var file = new LightSourceTransferData("Portable", new AxisymmetricSourceProfileDefinition(), fileFrame, 0, Vector3.Zero, [localRay]);

        var canonical = service.Import(file, ImportedSourcePosePolicy.CanonicalIdentityFrame);

        Assert.Equal(new Vector3(5, -2, 3), file.Frame.Position);
        Assert.Equal(new Point3(0, 0, 0), canonical.SourceFrame.Origin);
        AssertOrientationEquivalent(Quaternion.Identity, TransferredLightSourcePoseService.GetOrientation(canonical.SourceFrame));
        AssertVectorClose(localRay.PositionLocal, canonical.ExactRays[0].Origin);
        AssertVectorClose(localRay.DirectionLocal, canonical.ExactRays[0].Direction);

        var scenes = new SceneCollectionService();
        var scene = scenes.CreateScene("Scene 1");
        var library = scenes.AddToLibrary(new CollisionSourceLibraryItemViewModel { TransferredSource = canonical });
        scenes.AssignSource(scene, library);
        scene.AssignedSource!.TransferredSource!.SourceFrame = TransferredLightSourcePoseService.CreateFrame(new Vector3(5, 0, 0), Quaternion.Identity);
        Assert.Equal(new Point3(0, 0, 0), library.TransferredSource!.SourceFrame.Origin);
    }

    [Fact]
    public void PreserveFrameImport_RestoresStoredWorldPoseAndRays()
    {
        var service = new LightSourceTransferService();
        var frame = new Frame3D(new Vector3(5, -2, 3), FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, 20, -30, 45));
        var localRay = new LightSourceTransferRay(new Vector3(1, 2, 3), Vector3.UnitY);
        var file = new LightSourceTransferData("Portable", new AxisymmetricSourceProfileDefinition(), frame, 0, Vector3.Zero, [localRay]);

        var preserved = service.Import(file, ImportedSourcePosePolicy.PreserveFileFrame);

        Assert.Equal(new Point3(5, -2, 3), preserved.SourceFrame.Origin);
        AssertOrientationEquivalent(frame.Orientation, TransferredLightSourcePoseService.GetOrientation(preserved.SourceFrame));
        AssertVectorClose(frame.TransformPointToWorld(localRay.PositionLocal), preserved.ExactRays[0].Origin);
        AssertVectorClose(Vector3.Normalize(frame.TransformDirectionToWorld(localRay.DirectionLocal)), preserved.ExactRays[0].Direction);
    }

    private static void AssertVectorClose(Vector3 expected, Vector3 actual) => Assert.InRange(Vector3.Distance(expected, actual), 0, 1e-5);
    private static void AssertOrientationEquivalent(Quaternion expected, Quaternion actual) => Assert.InRange(1 - Math.Abs(Quaternion.Dot(Quaternion.Normalize(expected), Quaternion.Normalize(actual))), 0, 1e-5);
}
