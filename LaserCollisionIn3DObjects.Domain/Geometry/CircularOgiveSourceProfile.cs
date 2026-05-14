using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed class CircularOgiveSourceProfile : IAxisymmetricSourceProfile
{
    private const float EndpointTolerance = 1e-4f;
    private const float SampleTolerance = 1e-5f;

    private readonly float _centerU;
    private readonly float _centerRadius;
    private readonly float _branchSign;

    public CircularOgiveSourceProfile(float radiusStart, float radiusEnd, float length, float arcRadius, OgiveCurvatureDirection curvatureDirection)
    {
        RadiusStart = AxisymmetricSourceProfile.EnsurePositive(radiusStart, nameof(radiusStart));
        RadiusEnd = AxisymmetricSourceProfile.EnsurePositive(radiusEnd, nameof(radiusEnd));
        Length = AxisymmetricSourceProfile.EnsurePositive(length, nameof(length));
        ArcRadius = AxisymmetricSourceProfile.EnsurePositive(arcRadius, nameof(arcRadius));
        CurvatureDirection = curvatureDirection;

        var deltaRadius = RadiusEnd - RadiusStart;
        var chordLength = MathF.Sqrt((Length * Length) + (deltaRadius * deltaRadius));
        if (ArcRadius < (0.5f * chordLength))
        {
            throw new ArgumentException("Ogive arc radius is too small to connect R1 and R2 across the given length.", nameof(arcRadius));
        }

        var midU = 0.5f * Length;
        var midRadius = 0.5f * (RadiusStart + RadiusEnd);
        var halfChord = 0.5f * chordLength;
        var h = MathF.Sqrt(MathF.Max(0f, (ArcRadius * ArcRadius) - (halfChord * halfChord)));

        var chordDirU = Length / chordLength;
        var chordDirR = deltaRadius / chordLength;

        var p1U = -chordDirR;
        var p1R = chordDirU;
        var p2U = chordDirR;
        var p2R = -chordDirU;

        var candidateA = BuildCandidate(midU + (h * p1U), midRadius + (h * p1R));
        var candidateB = BuildCandidate(midU + (h * p2U), midRadius + (h * p2R));
        var selected = SelectByCurvatureDirection(candidateA, candidateB, curvatureDirection, midRadius);

        _centerU = selected.CenterU;
        _centerRadius = selected.CenterRadius;
        _branchSign = selected.BranchSign;

        ValidateMonotonicAndPositive();
    }

    public float RadiusStart { get; }
    public float RadiusEnd { get; }
    public float Length { get; }
    public float ArcRadius { get; }
    public OgiveCurvatureDirection CurvatureDirection { get; }

    public float RadiusAt(float u)
    {
        AxisymmetricSourceProfile.EnsureUInRange(u, Length);
        return EvaluateRadiusUnchecked(u);
    }

    public float RadiusDerivativeAt(float u)
    {
        AxisymmetricSourceProfile.EnsureUInRange(u, Length);
        var sqrtArg = EvaluateSqrtArgument(u);
        var denom = MathF.Sqrt(MathF.Max(sqrtArg, 0f));
        if (denom <= SampleTolerance)
        {
            throw new InvalidOperationException("Ogive derivative is undefined at this parameter value.");
        }

        return -_branchSign * (u - _centerU) / denom;
    }

    public Vector3 EvaluateSurfacePoint(float u, float theta)
    {
        var radius = RadiusAt(u);
        return new Vector3(u, radius * MathF.Cos(theta), radius * MathF.Sin(theta));
    }

    public Vector3 EvaluateBaseDirection(float u, float theta)
    {
        var derivative = RadiusDerivativeAt(u);
        return Vector3.Normalize(new Vector3(-derivative, MathF.Cos(theta), MathF.Sin(theta)));
    }

    private static Candidate SelectByCurvatureDirection(Candidate a, Candidate b, OgiveCurvatureDirection direction, float linearMidRadius)
    {
        var outwardPreferred = direction == OgiveCurvatureDirection.Outward;
        var aScore = a.MidRadius - linearMidRadius;
        var bScore = b.MidRadius - linearMidRadius;

        if (outwardPreferred)
        {
            return aScore >= bScore ? a : b;
        }

        return aScore <= bScore ? a : b;
    }

    private Candidate BuildCandidate(float centerU, float centerRadius)
    {
        var signAtStart = ResolveBranchSign(centerU, centerRadius);
        var midpointRadius = EvaluateRadiusUnchecked(0.5f * Length, centerU, centerRadius, signAtStart);
        return new Candidate(centerU, centerRadius, signAtStart, midpointRadius);
    }

    private float ResolveBranchSign(float centerU, float centerRadius)
    {
        var plusStart = EvaluateRadiusUnchecked(0f, centerU, centerRadius, +1f);
        var minusStart = EvaluateRadiusUnchecked(0f, centerU, centerRadius, -1f);

        var plusError = MathF.Abs(plusStart - RadiusStart);
        var minusError = MathF.Abs(minusStart - RadiusStart);
        var sign = plusError <= minusError ? +1f : -1f;

        var endValue = EvaluateRadiusUnchecked(Length, centerU, centerRadius, sign);
        if (MathF.Abs(endValue - RadiusEnd) > EndpointTolerance)
        {
            throw new ArgumentException("Circular ogive arc branch does not match the requested endpoints.");
        }

        return sign;
    }

    private float EvaluateRadiusUnchecked(float u)
        => EvaluateRadiusUnchecked(u, _centerU, _centerRadius, _branchSign);

    private float EvaluateRadiusUnchecked(float u, float centerU, float centerRadius, float branchSign)
    {
        var sqrtArg = EvaluateSqrtArgument(u, centerU);
        if (sqrtArg < -SampleTolerance)
        {
            throw new ArgumentException("Circular ogive profile is not defined for all u in [0, Length].");
        }

        return centerRadius + (branchSign * MathF.Sqrt(MathF.Max(0f, sqrtArg)));
    }

    private float EvaluateSqrtArgument(float u)
        => EvaluateSqrtArgument(u, _centerU);

    private float EvaluateSqrtArgument(float u, float centerU)
    {
        var du = u - centerU;
        return (ArcRadius * ArcRadius) - (du * du);
    }

    private void ValidateMonotonicAndPositive()
    {
        const int samples = 100;
        var previous = RadiusAt(0f);

        for (var i = 1; i <= samples; i++)
        {
            var u = Length * (i / (float)samples);
            var current = RadiusAt(u);

            if (current <= 0f)
            {
                throw new ArgumentException("Circular ogive profile must stay positive over [0, Length].");
            }

            if (RadiusEnd > RadiusStart && (current + SampleTolerance) < previous)
            {
                throw new ArgumentException("Circular ogive profile must be monotonic non-decreasing for R2 > R1.");
            }

            if (RadiusEnd < RadiusStart && (current - SampleTolerance) > previous)
            {
                throw new ArgumentException("Circular ogive profile must be monotonic non-increasing for R2 < R1.");
            }

            previous = current;
        }
    }

    private sealed record Candidate(float CenterU, float CenterRadius, float BranchSign, float MidRadius);
}
