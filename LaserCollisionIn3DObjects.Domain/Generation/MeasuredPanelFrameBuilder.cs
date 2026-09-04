using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Generation;

/// <summary>Builds the ideal, right-handed panel frame fitted to measured top and left edges.</summary>
public static class MeasuredPanelFrameBuilder
{
    private const float MinimumEdgeLengthSquared = 1e-12f;

    public static MeasuredPanelFrame Create(Vector3 leftTop, Vector3 rightTop, Vector3 leftBottom)
    {
        var widthEdge = rightTop - leftTop;
        var downEdge = leftBottom - leftTop;
        if (!IsFinite(widthEdge) || !IsFinite(downEdge)
            || widthEdge.LengthSquared() <= MinimumEdgeLengthSquared
            || downEdge.LengthSquared() <= MinimumEdgeLengthSquared)
        {
            throw new ArgumentException("Measured panel edges must be finite and non-zero.");
        }

        var width = Vector3.Normalize(widthEdge);
        var downRaw = Vector3.Normalize(downEdge);
        var normalRaw = Vector3.Cross(width, downRaw);
        if (normalRaw.LengthSquared() <= MinimumEdgeLengthSquared)
        {
            throw new ArgumentException("Measured panel width and height edges must not be parallel.");
        }

        var normal = Vector3.Normalize(normalRaw);
        var down = Vector3.Normalize(Vector3.Cross(normal, width));
        if (Vector3.Dot(down, downRaw) < 0f)
        {
            down = -down;
            normal = -normal;
        }

        // System.Numerics stores the transformed local axes in the matrix rows.
        var matrix = new Matrix4x4(
            -normal.X, -normal.Y, -normal.Z, 0f,
            width.X, width.Y, width.Z, 0f,
            -down.X, -down.Y, -down.Z, 0f,
            0f, 0f, 0f, 1f);
        var orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
        return new MeasuredPanelFrame(width, down, normal, orientation);
    }

    public static MeasuredPanelFrame Create(
        Vector3 leftTop, Vector3 rightTop, Vector3 rightBottom, Vector3 leftBottom,
        float width, float height, PrismGenerationMethodology methodology)
        => methodology switch
        {
            PrismGenerationMethodology.LtRtAnchoredOrthogonal => Create(leftTop, rightTop, leftBottom),
            PrismGenerationMethodology.LtAnchoredFourPointBestFit => CreateBestFit(leftTop, rightTop, rightBottom, leftBottom, width, height),
            _ => throw new ArgumentOutOfRangeException(nameof(methodology)),
        };

    /// <summary>Solves the fixed-LT orthogonal Procrustes problem with Horn's quaternion eigensystem.</summary>
    public static MeasuredPanelFrame CreateBestFit(
        Vector3 leftTop, Vector3 rightTop, Vector3 rightBottom, Vector3 leftBottom,
        float width, float height)
    {
        if (!IsFinite(leftTop) || !IsFinite(rightTop) || !IsFinite(rightBottom) || !IsFinite(leftBottom)
            || !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentException("Measured panel corners and dimensions must be finite, with positive dimensions.");

        var rt = rightTop - leftTop;
        var lb = leftBottom - leftTop;
        var rb = rightBottom - leftTop;
        if (rt.LengthSquared() <= MinimumEdgeLengthSquared || lb.LengthSquared() <= MinimumEdgeLengthSquared
            || Vector3.Cross(rt, lb).LengthSquared() <= MinimumEdgeLengthSquared)
            throw new ArgumentException("Measured panel corners do not provide independent width and height geometry.");

        // H = sum(q p^T), for q={ (W,0,0), (0,H,0), (W,H,0) } and p measured from fixed LT.
        var h = new double[3, 3];
        Accumulate(h, new Vector3(width, 0, 0), rt);
        Accumulate(h, new Vector3(0, height, 0), lb);
        Accumulate(h, new Vector3(width, height, 0), rb);
        var k = BuildHornMatrix(h);
        var eigenvector = LargestEigenvector(k);
        var rotation = Quaternion.Normalize(new Quaternion((float)eigenvector[0], (float)eigenvector[1], (float)eigenvector[2], (float)eigenvector[3]));
        if (!IsFinite(rotation)) throw new ArgumentException("The measured panel fit did not produce a finite proper rotation.");

        var fittedWidth = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        var fittedDown = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));
        var normal = Vector3.Normalize(Vector3.Cross(fittedWidth, fittedDown));
        // Prism local X is -normal, local Y is width, and local Z is up (-down).
        var prismMatrix = new Matrix4x4(
            -normal.X, -normal.Y, -normal.Z, 0,
            fittedWidth.X, fittedWidth.Y, fittedWidth.Z, 0,
            -fittedDown.X, -fittedDown.Y, -fittedDown.Z, 0,
            0, 0, 0, 1);
        var orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(prismMatrix));
        var residuals = new PanelFitResiduals(0,
            Vector3.Distance(rightTop, leftTop + fittedWidth * width),
            Vector3.Distance(leftBottom, leftTop + fittedDown * height),
            Vector3.Distance(rightBottom, leftTop + fittedWidth * width + fittedDown * height));
        return new MeasuredPanelFrame(fittedWidth, fittedDown, normal, orientation, residuals);
    }

    private static void Accumulate(double[,] h, Vector3 q, Vector3 p)
    { for (var r = 0; r < 3; r++) for (var c = 0; c < 3; c++) h[r, c] += q[r] * p[c]; }

    private static double[,] BuildHornMatrix(double[,] h)
    {
        var sigma = h[0, 0] + h[1, 1] + h[2, 2];
        var z = new[] { h[1, 2] - h[2, 1], h[2, 0] - h[0, 2], h[0, 1] - h[1, 0] };
        var k = new double[4, 4];
        for (var r = 0; r < 3; r++) for (var c = 0; c < 3; c++) k[r, c] = h[r, c] + h[c, r] - (r == c ? sigma : 0);
        for (var i = 0; i < 3; i++) k[i, 3] = k[3, i] = z[i];
        k[3, 3] = sigma;
        return k;
    }

    private static double[] LargestEigenvector(double[,] matrix)
    {
        // Jacobi diagonalization is deterministic and robust for this symmetric 4x4 system.
        var a = (double[,])matrix.Clone(); var v = new double[4, 4];
        for (var i = 0; i < 4; i++) v[i, i] = 1;
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var p = 0; var q = 1; var largest = 0d;
            for (var i = 0; i < 4; i++) for (var j = i + 1; j < 4; j++) if (Math.Abs(a[i, j]) > largest) { largest = Math.Abs(a[i, j]); p = i; q = j; }
            if (largest < 1e-14) break;
            var angle = .5 * Math.Atan2(2 * a[p, q], a[q, q] - a[p, p]); var c = Math.Cos(angle); var s = Math.Sin(angle);
            for (var i = 0; i < 4; i++) { var aip = a[i, p]; var aiq = a[i, q]; a[i, p] = c * aip - s * aiq; a[i, q] = s * aip + c * aiq; }
            for (var i = 0; i < 4; i++) { var api = a[p, i]; var aqi = a[q, i]; a[p, i] = c * api - s * aqi; a[q, i] = s * api + c * aqi; }
            for (var i = 0; i < 4; i++) { var vip = v[i, p]; var viq = v[i, q]; v[i, p] = c * vip - s * viq; v[i, q] = s * vip + c * viq; }
        }
        var index = Enumerable.Range(0, 4).MaxBy(i => a[i, i]);
        return Enumerable.Range(0, 4).Select(i => v[i, index]).ToArray();
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool IsFinite(Quaternion value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
}

public enum PrismGenerationMethodology { LtRtAnchoredOrthogonal, LtAnchoredFourPointBestFit }

public readonly record struct PanelFitResiduals(float LeftTop, float RightTop, float LeftBottom, float RightBottom)
{
    public float Rmse => MathF.Sqrt((RightTop * RightTop + LeftBottom * LeftBottom + RightBottom * RightBottom) / 3f);
}

public readonly record struct MeasuredPanelFrame(
    Vector3 Width,
    Vector3 Down,
    Vector3 Normal,
    Quaternion Orientation,
    PanelFitResiduals? Residuals = null);
