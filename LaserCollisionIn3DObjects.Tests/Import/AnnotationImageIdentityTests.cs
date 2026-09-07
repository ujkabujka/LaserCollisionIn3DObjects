using LaserCollisionIn3DObjects.Domain.Import;

namespace LaserCollisionIn3DObjects.Tests.Import;

public sealed class AnnotationImageIdentityTests
{
    [Theory]
    [InlineData("t1p1.jpg", 1, 1)]
    [InlineData("T1P16.JPG", 1, 16)]
    [InlineData("t2p3.jpg", 2, 3)]
    [InlineData("t12p105.png", 12, 105)]
    public void TryParse_RecognizesCompleteConvention(string fileName, int test, int panel)
    {
        Assert.True(AnnotationImageIdentity.TryParse(fileName, out var identity));
        Assert.Equal(test, identity.TestNumber);
        Assert.Equal(panel, identity.PanelNumber);
    }

    [Theory]
    [InlineData("prefix-t2p3.jpg")]
    [InlineData("t2p3-extra.jpg")]
    [InlineData("panel23.jpg")]
    public void TryParse_DoesNotExtractUnrelatedNumbers(string fileName)
        => Assert.False(AnnotationImageIdentity.TryParse(fileName, out _));

    [Fact]
    public void Comparer_OrdersByTestThenPanel_AndUsesNaturalFallbackLast()
    {
        string[] names = ["other10.jpg", "t2p3.jpg", "t1p10.jpg", "other2.jpg", "t2p1.jpg", "t1p16.jpg", "t1p2.jpg"];

        Assert.Equal(
            ["t1p2.jpg", "t1p10.jpg", "t1p16.jpg", "t2p1.jpg", "t2p3.jpg", "other2.jpg", "other10.jpg"],
            names.OrderBy(static name => name, AnnotationImageFileNameComparer.Instance));
    }
}
