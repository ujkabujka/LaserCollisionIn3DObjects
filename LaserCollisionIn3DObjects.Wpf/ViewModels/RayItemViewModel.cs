using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public sealed class RayItemViewModel : ObservableObject
{
    private float _originX;
    private float _originY;
    private float _originZ;
    private float _directionX = 1f;
    private float _directionY;
    private float _directionZ;

    public float OriginX
    {
        get => _originX;
        set => SetProperty(ref _originX, value);
    }

    public float OriginY
    {
        get => _originY;
        set => SetProperty(ref _originY, value);
    }

    public float OriginZ
    {
        get => _originZ;
        set => SetProperty(ref _originZ, value);
    }

    public float DirectionX
    {
        get => _directionX;
        set => SetProperty(ref _directionX, value);
    }

    public float DirectionY
    {
        get => _directionY;
        set => SetProperty(ref _directionY, value);
    }

    public float DirectionZ
    {
        get => _directionZ;
        set => SetProperty(ref _directionZ, value);
    }

    public override string ToString() => $"O({OriginX:F1},{OriginY:F1},{OriginZ:F1}) D({DirectionX:F1},{DirectionY:F1},{DirectionZ:F1})";
}
