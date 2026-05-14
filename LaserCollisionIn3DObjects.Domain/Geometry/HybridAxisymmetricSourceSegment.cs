namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed class HybridAxisymmetricSourceSegment
{
    public HybridAxisymmetricSourceSegment(HybridAxisymmetricSourceSegmentDefinition definition, float startU, IAxisymmetricSourceProfile profile)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        StartU = startU;
        EndU = startU + profile.Length;
    }

    public HybridAxisymmetricSourceSegmentDefinition Definition { get; }
    public float StartU { get; }
    public float EndU { get; }
    public IAxisymmetricSourceProfile Profile { get; }
}
