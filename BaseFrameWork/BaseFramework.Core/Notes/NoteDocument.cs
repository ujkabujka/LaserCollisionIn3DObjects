namespace BaseFramework.Core.Notes;

public sealed class NoteDocument
{
    public NoteDocument(string text = "")
    {
        Text = text ?? string.Empty;
    }

    public string Text { get; private set; } = string.Empty;

    public void Update(string text)
        => Text = text ?? string.Empty;

    public NoteDocument Clone()
        => new(Text);

    public override string ToString()
        => Text;
}
