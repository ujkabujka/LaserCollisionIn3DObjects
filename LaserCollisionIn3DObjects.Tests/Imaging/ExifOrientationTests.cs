using LaserCollisionIn3DObjects.Domain.Imaging;

namespace LaserCollisionIn3DObjects.Tests.Imaging;

public sealed class ExifOrientationTests
{
    [Theory]
    [InlineData(null, ExifOrientation.Normal)]
    [InlineData((ushort)0, ExifOrientation.Normal)]
    [InlineData((ushort)1, ExifOrientation.Normal)]
    [InlineData((ushort)6, ExifOrientation.Rotate90Clockwise)]
    [InlineData((ushort)8, ExifOrientation.Rotate270Clockwise)]
    [InlineData((ushort)99, ExifOrientation.Normal)]
    public void Normalize_UsesNormalForMissingMalformedOrUnknownValues(ushort? value, ExifOrientation expected) => Assert.Equal(expected, ExifOrientationHelper.Normalize(value));

    [Theory]
    [InlineData(ExifOrientation.Normal, false)]
    [InlineData(ExifOrientation.MirrorHorizontal, false)]
    [InlineData(ExifOrientation.Rotate180, false)]
    [InlineData(ExifOrientation.MirrorVertical, false)]
    [InlineData(ExifOrientation.MirrorHorizontalRotate270Clockwise, true)]
    [InlineData(ExifOrientation.Rotate90Clockwise, true)]
    [InlineData(ExifOrientation.MirrorHorizontalRotate90Clockwise, true)]
    [InlineData(ExifOrientation.Rotate270Clockwise, true)]
    public void SwapsDimensions_MatchesAllExifOrientations(ExifOrientation orientation, bool expected) => Assert.Equal(expected, ExifOrientationHelper.SwapsDimensions(orientation));
}
