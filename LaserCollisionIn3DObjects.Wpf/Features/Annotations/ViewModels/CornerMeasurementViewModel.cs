using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public enum CornerType
{
    LeftTop,
    RightTop,
    RightBottom,
    LeftBottom,
}

public enum CornerMeasurementMode
{
    Unspecified,
    ManualMeasurement,
    DirectCoordinateTheodolite,
}

public sealed class CornerMeasurementViewModel : ObservableObject
{
    private CornerMeasurementMode _selectedMode;
    private double? _manualAzimuthDeg;
    private double? _manualElevationDeg;
    private double? _manualDistanceMeters;
    private double? _directX;
    private double? _directY;
    private double? _directZ;

    public CornerMeasurementViewModel(CornerType cornerType)
    {
        CornerType = cornerType;
    }

    public CornerType CornerType { get; }

    public string DisplayName => CornerType switch
    {
        CornerType.LeftTop => "Left Top",
        CornerType.RightTop => "Right Top",
        CornerType.RightBottom => "Right Bottom",
        CornerType.LeftBottom => "Left Bottom",
        _ => CornerType.ToString(),
    };

    public CornerMeasurementMode SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public double? ManualAzimuthDeg
    {
        get => _manualAzimuthDeg;
        set => SetProperty(ref _manualAzimuthDeg, value);
    }

    public double? ManualElevationDeg
    {
        get => _manualElevationDeg;
        set => SetProperty(ref _manualElevationDeg, value);
    }

    public double? ManualDistanceMeters
    {
        get => _manualDistanceMeters;
        set => SetProperty(ref _manualDistanceMeters, value);
    }

    public double? DirectX
    {
        get => _directX;
        set => SetProperty(ref _directX, value);
    }

    public double? DirectY
    {
        get => _directY;
        set => SetProperty(ref _directY, value);
    }

    public double? DirectZ
    {
        get => _directZ;
        set => SetProperty(ref _directZ, value);
    }
}
