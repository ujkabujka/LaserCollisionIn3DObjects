using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Services;

/// <summary>Converts workspace sources to the portable domain text model without duplicating ray generation.</summary>
public sealed class LightSourceTextTransferService
{
    private readonly CylindricalRayGenerator _cylinder = new();
    private readonly AxisymmetricRayGenerator _axisymmetric = new();
    public LightSourceTextFile Export(CylindricalLightSourceItemViewModel source)
    {
        var frame = new Frame3D(new(source.PositionX,source.PositionY,source.PositionZ),FrameOrientationBuilder.ApplyLocalEulerDegrees(source.BaseOrientation,source.RotationX,source.RotationY,source.RotationZ));
        var profile=BuildProfile(source); var tilt=new Vector3(source.TiltPointX,source.TiltPointY,source.TiltPointZ);
        var rays=source.SourceKind==AxisymmetricSourceKind.Cylinder ? _cylinder.Generate(new CylindricalLightSource(source.Name,frame,source.Radius,source.Height,source.RayCount,source.TiltWeight,tilt)) : _axisymmetric.Generate(new AxisymmetricLightSource(source.Name,frame,source.SourceKind,profile.BuildProfile(),source.RayCount,source.TiltWeight,tilt));
        return Create(source.Name,profile,frame,source.TiltWeight,tilt,rays);
    }
    public LightSourceTextFile Export(ProjectedLightSourceItemViewModel source)
    {
        var frame=BuildFrame(source.SourceFrame,source.BaseOrientation); var rays=source.ExactRays.Count>0?source.ExactRays:source.Rays.Select(x=>x.Ray).ToList();
        return Create(source.Name,source.ProfileDefinition,frame,0,Vector3.Zero,rays);
    }
    public ProjectedLightSourceItemViewModel Import(LightSourceTextFile file)
    {
        var x=file.Frame.TransformDirectionToWorld(Vector3.UnitX);var y=file.Frame.TransformDirectionToWorld(Vector3.UnitY);var z=file.Frame.TransformDirectionToWorld(Vector3.UnitZ);
        var source=new ProjectedLightSourceItemViewModel { Name=file.Name, ProfileDefinition=file.Profile, OriginKind=ProjectedLightSourceOriginKind.ImportedTextFile, BaseOrientation=file.Frame.Orientation, SourceFrame=new PointSourceFrameState { Origin=new Point3(file.Frame.Position.X,file.Frame.Position.Y,file.Frame.Position.Z),AxisX=new Vector3D(x.X,x.Y,x.Z),AxisY=new Vector3D(y.X,y.Y,y.Z),AxisZ=new Vector3D(z.X,z.Y,z.Z)}};
        foreach(var ray in file.Rays) source.ExactRays.Add(new Ray3D(file.Frame.TransformPointToWorld(ray.PositionLocal),Vector3.Normalize(file.Frame.TransformDirectionToWorld(ray.DirectionLocal)))); return source;
    }
    private static LightSourceTextFile Create(string name,AxisymmetricSourceProfileDefinition p,Frame3D f,float tilt,Vector3 point,IEnumerable<Ray3D> rays)=>new(name,p,f,tilt,point,rays.Select(r=>new LightSourceTextRay(f.TransformPointToLocal(r.Origin),Vector3.Normalize(f.TransformDirectionToLocal(r.Direction)))).ToList());
    private static AxisymmetricSourceProfileDefinition BuildProfile(CylindricalLightSourceItemViewModel s)=>new(){Kind=s.SourceKind,Radius=s.Radius,Height=s.Height,Length=s.Length,RadiusStart=s.RadiusStart,RadiusEnd=s.RadiusEnd,ArcRadius=s.ArcRadius,OgiveCurvatureDirection=s.OgiveCurvatureDirection,Hybrid=s.HybridSegments.Select(x=>new HybridAxisymmetricSourceSegmentDefinition(x.SegmentKind,x.Length,x.RadiusStart,x.RadiusEnd,x.SegmentKind==HybridAxisymmetricSourceSegmentKind.CircularOgive?x.ArcRadius:null,x.OgiveCurvatureDirection)).ToList()};
    private static Frame3D BuildFrame(PointSourceFrameState s,Quaternion fallback){var x=new Vector3((float)s.AxisX.X,(float)s.AxisX.Y,(float)s.AxisX.Z);var y=new Vector3((float)s.AxisY.X,(float)s.AxisY.Y,(float)s.AxisY.Z);var z=new Vector3((float)s.AxisZ.X,(float)s.AxisZ.Y,(float)s.AxisZ.Z);var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);var q=Quaternion.CreateFromRotationMatrix(m);return new Frame3D(new((float)s.Origin.X,(float)s.Origin.Y,(float)s.Origin.Z),float.IsFinite(q.X)?q:fallback);}
}
