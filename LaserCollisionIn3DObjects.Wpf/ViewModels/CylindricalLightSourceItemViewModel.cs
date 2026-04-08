using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public sealed class CylindricalLightSourceItemViewModel : ObservableObject
{
    private string _name = "Light Source";
    private float _positionX;
    private float _positionY;
    private float _positionZ;
    private float _rotationX;
    private float _rotationY;
    private float _rotationZ;
    private float _radius = 5f;
    private float _height = 10f;
    private int _rayCount = 100;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public float PositionX { get => _positionX; set => SetProperty(ref _positionX, value); }
    public float PositionY { get => _positionY; set => SetProperty(ref _positionY, value); }
    public float PositionZ { get => _positionZ; set => SetProperty(ref _positionZ, value); }
    public float RotationX { get => _rotationX; set => SetProperty(ref _rotationX, value); }
    public float RotationY { get => _rotationY; set => SetProperty(ref _rotationY, value); }
    public float RotationZ { get => _rotationZ; set => SetProperty(ref _rotationZ, value); }
    public float Radius { get => _radius; set => SetProperty(ref _radius, value); }
    public float Height { get => _height; set => SetProperty(ref _height, value); }
    public int RayCount { get => _rayCount; set => SetProperty(ref _rayCount, value); }

    public override string ToString() => Name;
}
