using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Domain.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;

public sealed record AnnotationImageLoadResult(
    BitmapSource Image,
    int RawPixelWidth,
    int RawPixelHeight,
    ExifOrientation Orientation);

/// <summary>Decodes annotation images into memory and normalizes their EXIF display orientation.</summary>
public sealed class AnnotationImageLoader
{
    public AnnotationImageLoadResult Load(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault() ?? throw new InvalidOperationException("The image contains no decodable frames.");
        var orientation = ExifOrientationHelper.Normalize(ReadExifOrientation(frame.Metadata as BitmapMetadata));
        var normalized = ApplyExifOrientation(frame, orientation);
        if (normalized.CanFreeze) normalized.Freeze();
        return new AnnotationImageLoadResult(normalized, frame.PixelWidth, frame.PixelHeight, orientation);
    }

    internal static ushort? ReadExifOrientation(BitmapMetadata? metadata)
    {
        if (metadata is null) return null;
        try
        {
            var value = metadata.GetQuery("/app1/ifd/{ushort=274}");
            return value switch
            {
                ushort orientation => orientation,
                short orientation when orientation > 0 => (ushort)orientation,
                uint orientation when orientation <= ushort.MaxValue => (ushort)orientation,
                _ => null,
            };
        }
        catch (Exception) when (metadata is not null)
        {
            return null;
        }
    }

    internal static BitmapSource ApplyExifOrientation(BitmapSource source, ExifOrientation orientation)
    {
        ArgumentNullException.ThrowIfNull(source);
        var transform = CreateTransform(orientation);
        return transform is null ? source : new TransformedBitmap(source, transform);
    }

    private static Transform? CreateTransform(ExifOrientation orientation) => orientation switch
    {
        ExifOrientation.Normal => null,
        ExifOrientation.MirrorHorizontal => new ScaleTransform(-1, 1),
        ExifOrientation.Rotate180 => new RotateTransform(180),
        ExifOrientation.MirrorVertical => new ScaleTransform(1, -1),
        ExifOrientation.MirrorHorizontalRotate270Clockwise => CreateTransformGroup(new ScaleTransform(-1, 1), new RotateTransform(270)),
        ExifOrientation.Rotate90Clockwise => new RotateTransform(90),
        ExifOrientation.MirrorHorizontalRotate90Clockwise => CreateTransformGroup(new ScaleTransform(-1, 1), new RotateTransform(90)),
        ExifOrientation.Rotate270Clockwise => new RotateTransform(270),
        _ => null,
    };

    private static TransformGroup CreateTransformGroup(params Transform[] transforms)
    {
        var group = new TransformGroup();
        foreach (var transform in transforms) group.Children.Add(transform);
        return group;
    }
}
