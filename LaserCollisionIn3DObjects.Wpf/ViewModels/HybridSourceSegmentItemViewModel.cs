using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public sealed class HybridSourceSegmentItemViewModel : ObservableObject
{
    private HybridAxisymmetricSourceSegmentKind _segmentKind = HybridAxisymmetricSourceSegmentKind.Cylinder;
    private float _length = 5f;
    private float _radiusStart = 5f;
    private float _radiusEnd = 5f;
    private float _arcRadius = 20f;
    private OgiveCurvatureDirection _ogiveCurvatureDirection = OgiveCurvatureDirection.Outward;

    private int _segmentIndex = 1;
    private bool _isRadiusStartEditable = true;

    public int SegmentIndex
    {
        get => _segmentIndex;
        set
        {
            if (SetProperty(ref _segmentIndex, value))
            {
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public bool IsRadiusStartEditable
    {
        get => _isRadiusStartEditable;
        set => SetProperty(ref _isRadiusStartEditable, value);
    }

    public HybridAxisymmetricSourceSegmentKind SegmentKind
    {
        get => _segmentKind;
        set
        {
            if (SetProperty(ref _segmentKind, value))
            {
                RaisePropertyChanged(nameof(IsOgive));
                RaisePropertyChanged(nameof(IsCylinder));
                RaisePropertyChanged(nameof(IsConicalFrustum));
                RaisePropertyChanged(nameof(IsCircularOgive));
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public float Length
    {
        get => _length;
        set
        {
            if (SetProperty(ref _length, value))
            {
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public float RadiusStart
    {
        get => _radiusStart;
        set
        {
            if (SetProperty(ref _radiusStart, value))
            {
                RaisePropertyChanged(nameof(Summary));
                RaisePropertyChanged(nameof(Radius));
            }
        }
    }

    public float RadiusEnd
    {
        get => _radiusEnd;
        set
        {
            if (SetProperty(ref _radiusEnd, value))
            {
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public float ArcRadius
    {
        get => _arcRadius;
        set
        {
            if (SetProperty(ref _arcRadius, value))
            {
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public OgiveCurvatureDirection OgiveCurvatureDirection
    {
        get => _ogiveCurvatureDirection;
        set
        {
            if (SetProperty(ref _ogiveCurvatureDirection, value))
            {
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public bool IsOgive => SegmentKind == HybridAxisymmetricSourceSegmentKind.CircularOgive;
    public bool IsCylinder => SegmentKind == HybridAxisymmetricSourceSegmentKind.Cylinder;
    public bool IsConicalFrustum => SegmentKind == HybridAxisymmetricSourceSegmentKind.ConicalFrustum;
    public bool IsCircularOgive => SegmentKind == HybridAxisymmetricSourceSegmentKind.CircularOgive;

    public float Radius
    {
        get => RadiusStart;
        set
        {
            RadiusStart = value;
            RadiusEnd = value;
        }
    }

    public string Summary => SegmentKind switch
    {
        HybridAxisymmetricSourceSegmentKind.Cylinder => $"#{SegmentIndex} Cylinder | L={Length:G} | R={RadiusStart:G}",
        HybridAxisymmetricSourceSegmentKind.ConicalFrustum => $"#{SegmentIndex} Conical Frustum | L={Length:G} | R1={RadiusStart:G} \u2192 R2={RadiusEnd:G}",
        HybridAxisymmetricSourceSegmentKind.CircularOgive => $"#{SegmentIndex} Circular Ogive | L={Length:G} | R1={RadiusStart:G} \u2192 R2={RadiusEnd:G} | {OgiveCurvatureDirection}",
        _ => $"#{SegmentIndex} Unknown",
    };
}
