namespace LaserCollisionIn3DObjects.Domain.Imaging;

/// <summary>EXIF tag 274 orientation values, normalized to the safe Normal fallback.</summary>
public enum ExifOrientation : ushort
{
    Normal = 1,
    MirrorHorizontal = 2,
    Rotate180 = 3,
    MirrorVertical = 4,
    MirrorHorizontalRotate270Clockwise = 5,
    Rotate90Clockwise = 6,
    MirrorHorizontalRotate90Clockwise = 7,
    Rotate270Clockwise = 8,
}

public static class ExifOrientationHelper
{
    public static ExifOrientation Normalize(ushort? value) => value is >= 1 and <= 8
        ? (ExifOrientation)value.Value
        : ExifOrientation.Normal;

    public static bool SwapsDimensions(ExifOrientation orientation) => orientation is
        ExifOrientation.MirrorHorizontalRotate270Clockwise or
        ExifOrientation.Rotate90Clockwise or
        ExifOrientation.MirrorHorizontalRotate90Clockwise or
        ExifOrientation.Rotate270Clockwise;
}
