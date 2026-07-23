using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Export;

/// <summary>Canonical, versioned representation of a portable light source text file.</summary>
public sealed record LightSourceTextFile(
    string Name, AxisymmetricSourceProfileDefinition Profile, Frame3D Frame,
    float TiltWeight, Vector3 TiltPointLocal, IReadOnlyList<LightSourceTextRay> Rays);
public sealed record LightSourceTextRay(Vector3 PositionLocal, Vector3 DirectionLocal);

/// <summary>Reads and writes LASER_SOURCE_FILE_VERSION 1 files using invariant, round-trip-safe values.</summary>
public static class LightSourceTextSerializer
{
    public const int Version = 1;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly Regex RayKey = new("^(Position|Direction)\\s+(\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Serialize(LightSourceTextFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        Validate(file);
        var b = new StringBuilder();
        b.AppendLine("LASER_SOURCE_FILE_VERSION: 1").AppendLine();
        Line(b, "NAME", file.Name); Line(b, "SOURCE_KIND", file.Profile.Kind); Line(b, "RAY_COUNT", file.Rays.Count);
        b.AppendLine().AppendLine("RAY_POSITION_COORDINATE_SYSTEM: SOURCE_LOCAL").AppendLine("RAY_DIRECTION_COORDINATE_SYSTEM: SOURCE_LOCAL").AppendLine();
        Vec(b, "FRAME_ORIGIN_WORLD", file.Frame.Position); Vec(b, "FRAME_AXIS_X_WORLD", file.Frame.TransformDirectionToWorld(Vector3.UnitX)); Vec(b, "FRAME_AXIS_Y_WORLD", file.Frame.TransformDirectionToWorld(Vector3.UnitY)); Vec(b, "FRAME_AXIS_Z_WORLD", file.Frame.TransformDirectionToWorld(Vector3.UnitZ));
        b.AppendLine(); Line(b, "TILT_WEIGHT", file.TiltWeight); Vec(b, "TILT_POINT_LOCAL", file.TiltPointLocal); b.AppendLine().AppendLine("GEOMETRY_BEGIN");
        Geometry(b, file.Profile); b.AppendLine("GEOMETRY_END").AppendLine().AppendLine("RAYS_BEGIN");
        for (var i=0;i<file.Rays.Count;i++) { Vec(b, $"Position {i+1}", file.Rays[i].PositionLocal); Vec(b, $"Direction {i+1}", Vector3.Normalize(file.Rays[i].DirectionLocal)); b.AppendLine(); }
        b.AppendLine("RAYS_END"); return b.ToString();
    }

    public static LightSourceTextFile Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        var lines=text.Replace("\r\n","\n").Replace('\r','\n').Split('\n'); var headers=new Dictionary<string,(string v,int l)>(StringComparer.OrdinalIgnoreCase); var geometry=new Dictionary<string,(string v,int l)>(StringComparer.OrdinalIgnoreCase); var rays=new Dictionary<int,(Vector3? p,Vector3? d,int l)>(); bool inGeometry=false,inRays=false,geoBegin=false,geoEnd=false,raysBegin=false,raysEnd=false;
        for(var i=0;i<lines.Length;i++) { var raw=lines[i].Trim(); if(raw.Length==0||raw.StartsWith('#'))continue; var line=i+1;
            if(raw.Equals("GEOMETRY_BEGIN",StringComparison.OrdinalIgnoreCase)){if(geoBegin) Fail(line,"duplicate GEOMETRY_BEGIN"); geoBegin=true;inGeometry=true;continue;} if(raw.Equals("GEOMETRY_END",StringComparison.OrdinalIgnoreCase)){geoEnd=true;inGeometry=false;continue;} if(raw.Equals("RAYS_BEGIN",StringComparison.OrdinalIgnoreCase)){raysBegin=true;inRays=true;continue;} if(raw.Equals("RAYS_END",StringComparison.OrdinalIgnoreCase)){raysEnd=true;inRays=false;continue;}
            var colon=raw.IndexOf(':'); if(colon<1) Fail(line,"expected KEY: VALUE"); var key=raw[..colon].Trim();var value=raw[(colon+1)..].Trim();
            if(inRays) { var m=RayKey.Match(key); if(!m.Success) Fail(line,$"invalid ray field '{key}'"); var index=ParseInt(m.Groups[2].Value,line,"ray index"); if(index<1)Fail(line,"ray index must be one-based"); var vector=ParseVector(value,line,key); rays.TryGetValue(index,out var item); if(m.Groups[1].Value.Equals("Position",StringComparison.OrdinalIgnoreCase)){if(item.p is not null)Fail(line,$"duplicate Position {index}");item.p=vector;}else {if(item.d is not null)Fail(line,$"duplicate Direction {index}");item.d=vector;} item.l=line;rays[index]=item; }
            else { var target=inGeometry?geometry:headers; if(!target.TryAdd(key,(value,line)))Fail(line,$"duplicate field '{key}'"); }
        }
        if(!geoBegin||!geoEnd||!raysBegin||!raysEnd) throw new FormatException("The file must contain GEOMETRY_BEGIN, GEOMETRY_END, RAYS_BEGIN, and RAYS_END.");
        var version=Required(headers,"LASER_SOURCE_FILE_VERSION"); if(ParseInt(version.v,version.l,"LASER_SOURCE_FILE_VERSION")!=Version) throw new FormatException($"Unsupported light source file version: {version.v}.");
        var name=Required(headers,"NAME").v; var kindText=Required(headers,"SOURCE_KIND"); if(!Enum.TryParse<AxisymmetricSourceKind>(kindText.v,true,out var kind)) Fail(kindText.l,"invalid SOURCE_KIND"); var count=ParseInt(Required(headers,"RAY_COUNT").v,Required(headers,"RAY_COUNT").l,"RAY_COUNT"); if(count<0)throw new FormatException("RAY_COUNT must be non-negative.");
        var origin=ParseVector(Required(headers,"FRAME_ORIGIN_WORLD").v,Required(headers,"FRAME_ORIGIN_WORLD").l,"FRAME_ORIGIN_WORLD"); var x=ParseVector(Required(headers,"FRAME_AXIS_X_WORLD").v,Required(headers,"FRAME_AXIS_X_WORLD").l,"FRAME_AXIS_X_WORLD");var y=ParseVector(Required(headers,"FRAME_AXIS_Y_WORLD").v,Required(headers,"FRAME_AXIS_Y_WORLD").l,"FRAME_AXIS_Y_WORLD");var z=ParseVector(Required(headers,"FRAME_AXIS_Z_WORLD").v,Required(headers,"FRAME_AXIS_Z_WORLD").l,"FRAME_AXIS_Z_WORLD"); var frame=CreateFrame(origin,x,y,z);
        var profile=Profile(kind,geometry); var tilt= headers.TryGetValue("TILT_WEIGHT",out var tw)?ParseFloat(tw.v,tw.l,"TILT_WEIGHT"):0f; var tiltPoint=headers.TryGetValue("TILT_POINT_LOCAL",out var tp)?ParseVector(tp.v,tp.l,"TILT_POINT_LOCAL"):Vector3.Zero;
        if(rays.Count!=count) throw new FormatException($"Ray count mismatch: header declares {count} rays but {rays.Count} were parsed."); var result=new List<LightSourceTextRay>(count);for(var n=1;n<=count;n++){if(!rays.TryGetValue(n,out var ray)||ray.p is null||ray.d is null)throw new FormatException($"Ray {n} must contain exactly one Position {n} and Direction {n}.");if(ray.d.Value.LengthSquared()<=0)throw new FormatException($"Invalid Direction {n} at line {ray.l}: direction vector must be non-zero.");result.Add(new(ray.p.Value,Vector3.Normalize(ray.d.Value)));} var file=new LightSourceTextFile(name,profile,frame,tilt,tiltPoint,result);Validate(file);return file;
    }
    private static void Geometry(StringBuilder b,AxisymmetricSourceProfileDefinition p){ if(p.Kind==AxisymmetricSourceKind.Cylinder){Line(b,"RADIUS",p.Radius);Line(b,"LENGTH",p.Length>0?p.Length:p.Height);return;} if(p.Kind!=AxisymmetricSourceKind.Hybrid){Line(b,"RADIUS_START",p.RadiusStart);Line(b,"RADIUS_END",p.RadiusEnd);Line(b,"LENGTH",p.Length);if(p.Kind==AxisymmetricSourceKind.CircularOgive){Line(b,"ARC_RADIUS",p.ArcRadius);Line(b,"CURVATURE_DIRECTION",p.OgiveCurvatureDirection);}return;}Line(b,"SEGMENT_COUNT",p.Hybrid.Count);for(var i=0;i<p.Hybrid.Count;i++){var s=p.Hybrid[i];var n=$"SEGMENT_{i+1}_";Line(b,n+"KIND",s.SegmentKind);Line(b,n+"LENGTH",s.Length);Line(b,n+"RADIUS_START",s.RadiusStart);Line(b,n+"RADIUS_END",s.RadiusEnd);Line(b,n+"ARC_RADIUS",s.ArcRadius?.ToString("R",Culture)??"NONE");Line(b,n+"CURVATURE_DIRECTION",s.OgiveCurvatureDirection);}}
    private static AxisymmetricSourceProfileDefinition Profile(AxisymmetricSourceKind k,Dictionary<string,(string v,int l)> g){float F(string key){var a=Required(g,key);return ParseFloat(a.v,a.l,key);} if(k==AxisymmetricSourceKind.Cylinder)return new(){Kind=k,Radius=F("RADIUS"),Length=F("LENGTH")};if(k==AxisymmetricSourceKind.ConicalFrustum)return new(){Kind=k,RadiusStart=F("RADIUS_START"),RadiusEnd=F("RADIUS_END"),Length=F("LENGTH")};if(k==AxisymmetricSourceKind.CircularOgive){var c=Required(g,"CURVATURE_DIRECTION");if(!Enum.TryParse<OgiveCurvatureDirection>(c.v,true,out var d))Fail(c.l,"invalid CURVATURE_DIRECTION");return new(){Kind=k,RadiusStart=F("RADIUS_START"),RadiusEnd=F("RADIUS_END"),Length=F("LENGTH"),ArcRadius=F("ARC_RADIUS"),OgiveCurvatureDirection=d};}var count=ParseInt(Required(g,"SEGMENT_COUNT").v,Required(g,"SEGMENT_COUNT").l,"SEGMENT_COUNT");var a=new List<HybridAxisymmetricSourceSegmentDefinition>();for(var i=1;i<=count;i++){var n=$"SEGMENT_{i}_";var kind=Required(g,n+"KIND");if(!Enum.TryParse<HybridAxisymmetricSourceSegmentKind>(kind.v,true,out var sk))Fail(kind.l,"invalid "+n+"KIND");float? arc=g.TryGetValue(n+"ARC_RADIUS",out var av)&&!av.v.Equals("NONE",StringComparison.OrdinalIgnoreCase)?ParseFloat(av.v,av.l,n+"ARC_RADIUS"):null;var cv=Required(g,n+"CURVATURE_DIRECTION");if(!Enum.TryParse<OgiveCurvatureDirection>(cv.v,true,out var cd))Fail(cv.l,"invalid "+n+"CURVATURE_DIRECTION");a.Add(new(sk,F(n+"LENGTH"),F(n+"RADIUS_START"),F(n+"RADIUS_END"),arc,cd));}return new(){Kind=k,Hybrid=a};}
    private static Frame3D CreateFrame(Vector3 o,Vector3 x,Vector3 y,Vector3 z){if(x.LengthSquared()<1e-10||y.LengthSquared()<1e-10||z.LengthSquared()<1e-10)throw new FormatException("Invalid source frame: axes must be non-zero.");x=Vector3.Normalize(x);y=Vector3.Normalize(y);z=Vector3.Normalize(z);if(MathF.Abs(Vector3.Dot(x,y))>.001f||MathF.Abs(Vector3.Dot(x,z))>.001f||MathF.Abs(Vector3.Dot(y,z))>.001f||Vector3.Dot(Vector3.Cross(x,y),z)<.99f)throw new FormatException("Invalid source frame: axes must be orthogonal and right-handed.");var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);return new Frame3D(o,Quaternion.CreateFromRotationMatrix(m));}
    private static void Validate(LightSourceTextFile f){_ = f.Profile.BuildProfile();foreach(var r in f.Rays)if(!Finite(r.PositionLocal)||!Finite(r.DirectionLocal)||r.DirectionLocal.LengthSquared()<=0)throw new ArgumentException("Rays must contain finite positions and non-zero directions.");} private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);private static (string v,int l) Required(Dictionary<string,(string v,int l)> d,string k)=>d.TryGetValue(k,out var x)?x:throw new FormatException($"Required field '{k}' is missing.");private static Vector3 ParseVector(string s,int l,string f){var p=s.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(p.Length!=3)Fail(l,$"{f} must contain three numbers");return new(ParseFloat(p[0],l,f),ParseFloat(p[1],l,f),ParseFloat(p[2],l,f));}private static float ParseFloat(string s,int l,string f){if(!float.TryParse(s,NumberStyles.Float,Culture,out var v)||!float.IsFinite(v))Fail(l,$"invalid finite number for {f}");return v;}private static int ParseInt(string s,int l,string f){if(!int.TryParse(s,NumberStyles.Integer,Culture,out var v))Fail(l,$"invalid integer for {f}");return v;}private static void Fail(int l,string m)=>throw new FormatException($"Line {l}: {m}.");private static void Line(StringBuilder b,string k,object? v) => b.Append(k).Append(": ").Append(v switch { float f => f.ToString("R", Culture), double d => d.ToString("R", Culture), _ => Convert.ToString(v, Culture) }).AppendLine();private static void Vec(StringBuilder b,string k,Vector3 v)=>b.Append(k).Append(": ").Append(v.X.ToString("R",Culture)).Append(' ').Append(v.Y.ToString("R",Culture)).Append(' ').Append(v.Z.ToString("R",Culture)).AppendLine();
}
