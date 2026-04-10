using System.Windows;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Rectification;

public static class Homography
{
    public static double[] ComputeFromFourPointPairs(IReadOnlyList<Point> src, IReadOnlyList<Point> dst)
    {
        if (src.Count != 4 || dst.Count != 4)
        {
            throw new ArgumentException("Homography requires exactly four source and destination points.");
        }

        var a = new double[8, 8];
        var b = new double[8];

        for (var i = 0; i < 4; i++)
        {
            var x = src[i].X;
            var y = src[i].Y;
            var u = dst[i].X;
            var v = dst[i].Y;

            var r = i * 2;
            a[r, 0] = x; a[r, 1] = y; a[r, 2] = 1; a[r, 6] = -x * u; a[r, 7] = -y * u;
            b[r] = u;

            a[r + 1, 3] = x; a[r + 1, 4] = y; a[r + 1, 5] = 1; a[r + 1, 6] = -x * v; a[r + 1, 7] = -y * v;
            b[r + 1] = v;
        }

        var h = SolveLinearSystem(a, b);
        return [h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1.0];
    }

    public static Point Transform(Point p, double[] h)
    {
        var w = h[6] * p.X + h[7] * p.Y + h[8];
        if (Math.Abs(w) < 1e-8)
        {
            return new Point();
        }

        var x = (h[0] * p.X + h[1] * p.Y + h[2]) / w;
        var y = (h[3] * p.X + h[4] * p.Y + h[5]) / w;
        return new Point(x, y);
    }

    public static double[] Invert3x3(double[] m)
    {
        var det =
            m[0] * (m[4] * m[8] - m[5] * m[7]) -
            m[1] * (m[3] * m[8] - m[5] * m[6]) +
            m[2] * (m[3] * m[7] - m[4] * m[6]);

        if (Math.Abs(det) < 1e-10)
        {
            throw new InvalidOperationException("Homography matrix is singular.");
        }

        var invDet = 1.0 / det;
        return
        [
            (m[4] * m[8] - m[5] * m[7]) * invDet,
            (m[2] * m[7] - m[1] * m[8]) * invDet,
            (m[1] * m[5] - m[2] * m[4]) * invDet,
            (m[5] * m[6] - m[3] * m[8]) * invDet,
            (m[0] * m[8] - m[2] * m[6]) * invDet,
            (m[2] * m[3] - m[0] * m[5]) * invDet,
            (m[3] * m[7] - m[4] * m[6]) * invDet,
            (m[1] * m[6] - m[0] * m[7]) * invDet,
            (m[0] * m[4] - m[1] * m[3]) * invDet,
        ];
    }

    private static double[] SolveLinearSystem(double[,] a, double[] b)
    {
        var n = b.Length;
        var aug = new double[n, n + 1];

        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++)
            {
                aug[r, c] = a[r, c];
            }

            aug[r, n] = b[r];
        }

        for (var i = 0; i < n; i++)
        {
            var maxRow = i;
            for (var r = i + 1; r < n; r++)
            {
                if (Math.Abs(aug[r, i]) > Math.Abs(aug[maxRow, i]))
                {
                    maxRow = r;
                }
            }

            if (Math.Abs(aug[maxRow, i]) < 1e-12)
            {
                throw new InvalidOperationException("Cannot solve homography: degenerate point configuration.");
            }

            if (maxRow != i)
            {
                for (var c = i; c <= n; c++)
                {
                    (aug[i, c], aug[maxRow, c]) = (aug[maxRow, c], aug[i, c]);
                }
            }

            var pivot = aug[i, i];
            for (var c = i; c <= n; c++)
            {
                aug[i, c] /= pivot;
            }

            for (var r = 0; r < n; r++)
            {
                if (r == i)
                {
                    continue;
                }

                var factor = aug[r, i];
                for (var c = i; c <= n; c++)
                {
                    aug[r, c] -= factor * aug[i, c];
                }
            }
        }

        var x = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = aug[i, n];
        }

        return x;
    }
}
