using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public sealed class PrismItemViewModel : ObservableObject
{
    private string _name = "Prism";
    private float _positionX;
    private float _positionY;
    private float _positionZ;
    private float _rotationX;
    private float _rotationY;
    private float _rotationZ;
    private float _sizeX = 2f;
    private float _sizeY = 2f;
    private float _sizeZ = 2f;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public float PositionX
    {
        get => _positionX;
        set => SetProperty(ref _positionX, value);
    }

    public float PositionY
    {
        get => _positionY;
        set => SetProperty(ref _positionY, value);
    }

    public float PositionZ
    {
        get => _positionZ;
        set => SetProperty(ref _positionZ, value);
    }

    public float RotationX
    {
        get => _rotationX;
        set => SetProperty(ref _rotationX, value);
    }

    public float RotationY
    {
        get => _rotationY;
        set => SetProperty(ref _rotationY, value);
    }

    public float RotationZ
    {
        get => _rotationZ;
        set => SetProperty(ref _rotationZ, value);
    }

    public float SizeX
    {
        get => _sizeX;
        set => SetProperty(ref _sizeX, value);
    }

    public float SizeY
    {
        get => _sizeY;
        set => SetProperty(ref _sizeY, value);
    }

    public float SizeZ
    {
        get => _sizeZ;
        set => SetProperty(ref _sizeZ, value);
    }

    public override string ToString() => Name;
}
