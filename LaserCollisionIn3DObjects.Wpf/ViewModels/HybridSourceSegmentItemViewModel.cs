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

    public int SegmentIndex { get; set; }
    public bool IsRadiusStartEditable { get; set; }

    public HybridAxisymmetricSourceSegmentKind SegmentKind
    {
        get => _segmentKind;
        set
        {
            if (SetProperty(ref _segmentKind, value))
            {
                RaisePropertyChanged(nameof(IsOgive));
            }
        }
    }

    public float Length { get => _length; set => SetProperty(ref _length, value); }
    public float RadiusStart { get => _radiusStart; set => SetProperty(ref _radiusStart, value); }
    public float RadiusEnd { get => _radiusEnd; set => SetProperty(ref _radiusEnd, value); }
    public float ArcRadius { get => _arcRadius; set => SetProperty(ref _arcRadius, value); }
    public OgiveCurvatureDirection OgiveCurvatureDirection { get => _ogiveCurvatureDirection; set => SetProperty(ref _ogiveCurvatureDirection, value); }

    public bool IsOgive => SegmentKind == HybridAxisymmetricSourceSegmentKind.CircularOgive;
}
