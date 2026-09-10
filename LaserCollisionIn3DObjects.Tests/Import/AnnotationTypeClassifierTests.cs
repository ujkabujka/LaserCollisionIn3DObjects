using LaserCollisionIn3DObjects.Domain.Import;

namespace LaserCollisionIn3DObjects.Tests.Import;

public sealed class AnnotationTypeClassifierTests
{
    [Theory]
    [InlineData("1", AnnotationSemanticType.Hole)]
    [InlineData("2", AnnotationSemanticType.Natural)]
    [InlineData("3", AnnotationSemanticType.Panel)]
    [InlineData("panel", AnnotationSemanticType.Panel)]
    [InlineData("plane", AnnotationSemanticType.Panel)]
    public void Classify_UsesAuthoritativeViaTypeMapping(string type, AnnotationSemanticType expected)
        => Assert.Equal(expected, AnnotationTypeClassifier.Classify(type));

    [Theory]
    [InlineData("")]
    [InlineData("4")]
    [InlineData("unexpected")]
    public void Classify_UnknownTypesAreNotHoles(string type)
    {
        Assert.Equal(AnnotationSemanticType.Unknown, AnnotationTypeClassifier.Classify(type));
        Assert.False(AnnotationTypeClassifier.IsHole(type));
    }
}
