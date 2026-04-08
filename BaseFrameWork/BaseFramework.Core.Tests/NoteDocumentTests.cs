using BaseFramework.Core.Notes;

namespace BaseFramework.Core.Tests;

public class NoteDocumentTests
{
    [Fact]
    public void Update_ShouldReplaceContent()
    {
        var doc = new NoteDocument("hello");
        doc.Update("world");
        Assert.Equal("world", doc.Text);
    }

    [Fact]
    public void Clone_ShouldCopyContent()
    {
        var original = new NoteDocument("clone me");
        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Text, clone.Text);
    }
}
